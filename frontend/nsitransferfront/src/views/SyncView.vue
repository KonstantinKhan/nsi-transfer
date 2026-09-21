<template>
  <div class="sync-container">
    <div class="sync-content">
      <button class="sync-btn" @click="handleStartSync" :disabled="isSyncing">
        <svg v-if="isSyncing" class="spinner" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M21 12a9 9 0 1 1-6.219-8.56" />
        </svg>
        {{ isSyncing ? 'Запуск...' : 'Начать синхронизацию' }}
      </button>

      <StatusMessage ref="statusRef" />

      <div v-if="isLoadingList" class="loading-text">Загрузка истории синхронизаций...</div>

      <div v-else class="statistics-list">
        <SendingStatistics
          v-for="item in statistics"
          :key="item.id"
          :data="item"
        />
        <div v-if="statistics.length === 0" class="empty-state">
          Нет данных о синхронизациях
        </div>

        <!--
          Сентинел для infinite scroll.
          Рендерится только пока есть следующие страницы (hasMorePages).
          Как только он попадает во viewport — IntersectionObserver дёргает fetchNextPage().
        -->
        <div v-if="hasMorePages" ref="sentinelEl" class="scroll-sentinel">
          <span v-if="isLoadingMore" class="loading-text">Загрузка ещё...</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, reactive, watch, onMounted, onBeforeUnmount } from 'vue';
import SendingStatistics from '@/components/SendingStatistics.vue';
import StatusMessage from '@/components/StatusMessage.vue';
import {
  getSendings,
  startSync,
  listenForAllSyncEvents,
} from '@/services/polynomSyncService';
import type {
  SendingModel,
  MessageModel,
  AllSyncEventHandlers,
  StatusChangedPayload,
  MessageCreatedPayload,
  ObjectsCollectedPayload,
  MessagePublishedPayload,
  MessageFailedPayload,
  ErrorPayload,
  MessageEmptyPayload,
  SendingCompletedPayload,
  RetryObjectsCollectedPayload,
  RetryMessageCreatedPayload,
  RetryMessagePublishedPayload,
  RetryMessageEmptyPayload,
} from '@/types/sync.types';
import {
  SendingStatusEnum,
  SendingStatusEnumDescriptions,
  RabbitMqPublishingResultEnum,
  MessageTypeEnum,
} from '@/types/sync.types';

interface UIDSending {
  id: string;
  sendingId: string;
  status: string;
  statusId: SendingStatusEnum;
  // "Сырое" значение даты создания (ISO-строка) — нужно для сортировки и построения
  // курсора пагинации. ПРЕДПОЛОЖЕНИЕ: sending.initiatedAt и есть то самое поле,
  // по которому backend сортирует список ("дата создания"). Если на бэке это
  // отдельное поле (например CreatedAt, отличное от initiatedAt) — поменяй здесь.
  initiatedAtRaw: string;
  startTime: string;
  endTime: string;
  initiatorName: string;
  targetClassificationNodeName: string;
  messages: MessageModel[];
}

// ==== Контракт пагинации ====

interface GetSendingsResult {
  items: SendingModel[];
  hasMore: boolean; // true, если после этой страницы на сервере есть ещё записи
}

interface PageCursor {
  initiatedAt: string;
  id: string;
}

const PAGE_SIZE = 30;

const isSyncing = ref(false);
const statusRef = ref<InstanceType<typeof StatusMessage> | null>(null);
const isLoadingList = ref(true);
const isLoadingMore = ref(false);
const hasMorePages = ref(true);
const cursor = ref<PageCursor | null>(null);

// Единое хранилище сущностей: id -> UIDSending.
// Map вместо массива — чтобы upsert по id был идемпотентным: одна и та же сущность,
// пришедшая с любой страницы пагинации ИЛИ по SSE, просто перезаписывает саму себя
// в этом же слоте вместо создания дубликата.
const statisticsMap = reactive(new Map<string, UIDSending>());

// Отображаемый список всегда пересчитывается из карты и сортируется ТЕМИ ЖЕ полями
// и в том же направлении, что и backend (initiatedAt убыв., id как tie-breaker).
// Если это разойдётся с ORDER BY на сервере — курсор и порядок в UI разъедутся.
const statistics = computed<UIDSending[]>(() =>
  Array.from(statisticsMap.values()).sort((a, b) => {
    const dateDiff = b.initiatedAtRaw.localeCompare(a.initiatedAtRaw);
    if (dateDiff !== 0) return dateDiff;
    return b.id.localeCompare(a.id);
  })
);

// Раньше — карта EventSource на каждый sendingId.
// Теперь одно соединение обслуживает события сразу для всех отправлений.
const allEventsSource = ref<EventSource | null>(null);

// Сентинел-элемент для infinite scroll и его наблюдатель.
const sentinelEl = ref<HTMLElement | null>(null);
let sentinelObserver: IntersectionObserver | null = null;

// === Вспомогательные функции ===

const formatDateTimeWithMs = (dateString: string | null | undefined, hideDefaultEndDate: boolean = false): string => {
  if (!dateString) return '';
  if (hideDefaultEndDate && dateString.startsWith('0001-01-01')) return '';
  const date = new Date(dateString);
  if (isNaN(date.getTime())) return '';
  const pad = (n: number, len: number = 2) => String(n).padStart(len, '0');
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ` +
         `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`;
};

const mapSendingToUI = (sending: SendingModel): UIDSending => ({
  id: sending.id,
  sendingId: sending.id,
  status: SendingStatusEnumDescriptions[sending.statusId],
  statusId: sending.statusId,
  initiatedAtRaw: sending.initiatedAt,
  startTime: formatDateTimeWithMs(sending.initiatedAt),
  endTime: formatDateTimeWithMs(sending.endedAt, true) || (sending.endedAt ? '' : 'В процессе'),
  initiatorName: sending.initiatorName,
  targetClassificationNodeName: sending.targetReferenceNodeName,
  messages: (sending.messages || []),
});

const sendingStatusStringToEnum = (statusStr: string): SendingStatusEnum => {
  const key = statusStr as keyof typeof SendingStatusEnum;
  const value = SendingStatusEnum[key];
  return typeof value === 'number' ? (value as SendingStatusEnum) : SendingStatusEnum.Unknown;
};

const messageStatusStringToEnum = (statusSrt: string): RabbitMqPublishingResultEnum => {
  const key = statusSrt as keyof typeof RabbitMqPublishingResultEnum;
  const value = RabbitMqPublishingResultEnum[key];
  return typeof value === 'number' ? (value as RabbitMqPublishingResultEnum) : RabbitMqPublishingResultEnum.Unknown;
};

const findSending = (sendingId: string): UIDSending | undefined =>
  statisticsMap.get(sendingId);

const upsertSending = (sending: UIDSending) => {
  statisticsMap.set(sending.id, sending);
};

const findMessage = (sending: UIDSending, messageId: number): MessageModel | undefined =>
  sending.messages.find((m) => m.id === messageId);



// === Обработчики событий синхронизации ===

const handleStatusChanged = async (sendingId: string, payload: StatusChangedPayload) => {
  const sending = findSending(sendingId);
  if (!sending && payload.sendingModel !== undefined) {
    upsertSending(mapSendingToUI(payload.sendingModel));
  }
  if (!sending) return;
  const newStatusId = sendingStatusStringToEnum(payload.status);
  sending.statusId = newStatusId;
  sending.status = SendingStatusEnumDescriptions[newStatusId] || payload.status;
  if (payload.endedAt) sending.endTime = formatDateTimeWithMs(payload.endedAt, true);
};

const handleMessageCreated = (sendingId: string, payload: MessageCreatedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  sending.messages.push({
    id: payload.id,
    startedCollectionFromPolynomAt: payload.startedCollectionFromPolynomAt,
    finishedCollectionFromPolynomAt: null,
    sentAtQueue: null,
    polynomObjectsAmountInMessage: 0,
    publishingResultId: 0,
    messageFailure: null,
    messageType: MessageTypeEnum.FirstSend
  });
};

const handleObjectsCollected = (sendingId: string, payload: ObjectsCollectedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id);
  if (message) {
    message.polynomObjectsAmountInMessage = payload.objectsCount;
    message.finishedCollectionFromPolynomAt = payload.finishedCollectionFromPolynomAt;
  }
};

const handleMessageEmpty = (sendingId: string, payload: MessageEmptyPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const messageIndex = sending.messages.findIndex(m => m.id === payload.id);
  if (messageIndex !== -1) {
    sending.messages.splice(messageIndex, 1);
  }
};

const handleMessagePublished = (sendingId: string, payload: MessagePublishedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id);
  if (message) {
    message.sentAtQueue = payload.sentAtQueue;
    message.publishingResultId = RabbitMqPublishingResultEnum.Ack;
  }
};

const handleMessageFailed = (sendingId: string, payload: MessageFailedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id);
  if (message) {
    message.publishingResultId = messageStatusStringToEnum(payload.errorStatus);
    message.messageFailure = {
      messageId: message.id,
      failureReasonTitle: 'Ошибка синхронизации',
      failureDescription: payload.error,
      failedAt: new Date().toISOString(),
    };
  }
};

const handleSendingCompleted = (sendingId: string, payload: SendingCompletedPayload) => {
  const sending = findSending(sendingId);
  if (sending) {
    sending.statusId = SendingStatusEnum.Completed;
    sending.status = SendingStatusEnumDescriptions[SendingStatusEnum.Completed];
    sending.endTime = formatDateTimeWithMs(new Date(payload.endedAt).toISOString());
  }
};

const handleSyncError = (sendingId: string, payload: ErrorPayload) => {
  const sending = findSending(sendingId);
  if (sending) {
    sending.statusId = SendingStatusEnum.ErrorUnknown;
    sending.status = SendingStatusEnumDescriptions[SendingStatusEnum.ErrorUnknown];
    sending.endTime = formatDateTimeWithMs(new Date().toISOString());
  }
  console.error(`[${sendingId}] ❌ Критическая ошибка:`, payload.message);
  alert(`Ошибка синхронизации [${sendingId}]:\n${payload.message}`);
};

// <-- НОВЫЕ: Обработчики событий Retry
const handleRetryMessageCreated = (sendingId: string, payload: RetryMessageCreatedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;

  sending.messages.push({
    id: payload.id,
    startedCollectionFromPolynomAt: new Date().toISOString(),
    finishedCollectionFromPolynomAt: null,
    sentAtQueue: null,
    polynomObjectsAmountInMessage: 0,
    preparedObjectsCount: payload.objectsToRetryCount, // <-- Сохраняем общее число подготовленных
    publishingResultId: 0,
    messageFailure: null,
    messageType: MessageTypeEnum.Retry
  });
  console.log(`[${sendingId}] Создано сообщение-ретрай #${payload.id} (объектов к повтору: ${payload.objectsToRetryCount})`);
};

const handleRetryObjectsCollected = (sendingId: string, payload: RetryObjectsCollectedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id);
  if (message) {
    message.polynomObjectsAmountInMessage = payload.objectsCount; // Фактически отправлено
    message.finishedCollectionFromPolynomAt = payload.finishedCollectionFromPolynomAt;
    console.log(`[${sendingId}] Сообщение-ретрай #${payload.id}: успешно собрано и отправлено ${payload.objectsCount} объектов`);
  }
};

const handleRetryMessagePublished = (sendingId: string, payload: RetryMessagePublishedPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id);
  if (message) {
    message.sentAtQueue = payload.sentAtQueue;
    message.publishingResultId = RabbitMqPublishingResultEnum.Ack;
  }
};

const handleRetryMessageEmpty = (sendingId: string, payload: RetryMessageEmptyPayload) => {
  const sending = findSending(sendingId);
  if (!sending) return;
  const message = findMessage(sending, payload.id)
  if (message) {
    message.finishedCollectionFromPolynomAt = payload.finishedCollectionFromPolynomAt;
    message.publishingResultId = RabbitMqPublishingResultEnum.FailedDuringInformationCollection;
  }
  
};

const subscribeToAllSyncEvents = () => {
  if (allEventsSource.value) return;

  const handlers: AllSyncEventHandlers = {
    onStatusChanged: handleStatusChanged,
    onMessageCreated: handleMessageCreated,
    onObjectsCollected: handleObjectsCollected,
    onMessageEmpty: handleMessageEmpty,
    onMessagePublished: handleMessagePublished,
    onMessageFailed: handleMessageFailed,
    onSendingCompleted: handleSendingCompleted,
    onError: handleSyncError,
    onConnectionError: (e) => console.error('Ошибка общего SSE-соединения:', e),
    // <-- НОВЫЕ обработчики
    onRetryMessageCreated: handleRetryMessageCreated,
    onRetryObjectsCollected: handleRetryObjectsCollected,
    onRetryMessagePublished: handleRetryMessagePublished,
    onRetryMessageEmpty: handleRetryMessageEmpty,
  };

  allEventsSource.value = listenForAllSyncEvents(handlers);
  console.log("[Init] Подписка на серверные события активирована");
};



// === Обработчик кнопки запуска ===

const handleStartSync = async () => {
  isSyncing.value = true;
  try {
    await startSync();
  } catch (error) {
    console.error('Ошибка запуска:', error);
    alert(error instanceof Error ? error.message : 'Не удалось запустить синхронизацию');
  } finally {
    isSyncing.value = false;
  }
};



// === Загрузка сущностей Sendings (пагинированная) ===

const applyPage = (page: SendingModel[]) => {
  page.forEach((s) => upsertSending(mapSendingToUI(s)));
};

const updateCursorFromPage = (page: SendingModel[]) => {
  if (page.length === 0) return;
  // Курсор строится по ПОСЛЕДНЕМУ элементу именно этой страницы, полученной с сервера
  // (а не по последнему элементу в UI-списке!). Сервер отдаёт данные отсортированными
  // по дате создания, поэтому последний элемент страницы — самая "старая" запись,
  // загруженная на данный момент, и именно на неё нужно "продолжать" следующим запросом.
  const last = page[page.length - 1];
  if (!last) {
    console.warn('Невозможно построить курсор: не удаётся получить последний элемент страницы', last);
    return;
  }
  cursor.value = { initiatedAt: last.initiatedAt, id: last.id };
};

const fetchInitialSendings = async () => {
  isLoadingList.value = true;
  try {
    const { items, hasMore }: GetSendingsResult = await getSendings({ pageSize: PAGE_SIZE });
    applyPage(items);
    updateCursorFromPage(items);
    hasMorePages.value = hasMore;
    console.log(`[Init] Загружено ${items.length} синхронизаций`);
  } catch (error) {
    console.error('Ошибка загрузки истории:', error);
  } finally {
    isLoadingList.value = false;
  }
};

const fetchNextPage = async () => {
  // Защита: не грузим, если уже грузим, если страниц больше нет,
  // или если курсора ещё нет (значит первая страница ещё не пришла).
  if (isLoadingMore.value || !hasMorePages.value || !cursor.value) return;

  isLoadingMore.value = true;
  try {
    const { items, hasMore }: GetSendingsResult = await getSendings({
      pageSize: PAGE_SIZE,
      cursorInitiatedAt: cursor.value.initiatedAt,
      cursorId: cursor.value.id,
    });
    applyPage(items);
    updateCursorFromPage(items);
    hasMorePages.value = hasMore;
    console.log(`[Pagination] Догружено ${items.length} синхронизаций`);
  } catch (error) {
    console.error('Ошибка догрузки следующей страницы:', error);
  } finally {
    isLoadingMore.value = false;
  }
};

// === Infinite scroll через IntersectionObserver ===
// Наблюдаем за сентинел-элементом в конце списка. Когда он появляется в DOM
// (v-if="hasMorePages") — начинаем его наблюдать; когда пропадает — отписываемся.
// Это надёжнее, чем считать пиксели скролла вручную: браузер сам эффективно
// отслеживает пересечение с viewport, и логика не зависит от того, скроллится
// ли вся страница целиком или конкретный внутренний контейнер.
watch(sentinelEl, (newEl, oldEl) => {
  if (oldEl && sentinelObserver) {
    sentinelObserver.unobserve(oldEl);
  }

  if (newEl) {
    if (!sentinelObserver) {
      sentinelObserver = new IntersectionObserver(
        (entries) => {
          if (entries[0]?.isIntersecting) {
            fetchNextPage();
          }
        },
        {
          root: null,       // null = viewport. Если скроллится не window, а конкретный
                             // div (например .sync-content с overflow-y: auto) — укажи его сюда.
          rootMargin: '200px', // подгружаем чуть заранее, не дожидаясь упора в самый низ
          threshold: 0,
        }
      );
    }
    sentinelObserver.observe(newEl);
  }
}, { flush: 'post' });

// === Lifecycle ===

onMounted(() => {
  // Подписываемся до загрузки списка — чтобы не потерять события,
  // которые могут прийти, пока список ещё грузится.
  subscribeToAllSyncEvents();
  fetchInitialSendings();
});

onBeforeUnmount(() => {
  allEventsSource.value?.close();
  allEventsSource.value = null;
  sentinelObserver?.disconnect();
  sentinelObserver = null;
});
</script>

<style scoped src="./SyncView.css"></style>

<style scoped>
.scroll-sentinel {
  min-height: 1px;
  display: flex;
  justify-content: center;
  padding: 12px 0;
}
</style>