<template>
  <div class="sending-card">
    <div class="target-classification-node-name">
      <span class="target-classification-node-name-span">{{ data.targetClassificationNodeName }}</span>
    </div>
    <div class="sending-header" @click="toggleExpand">
      <div class="header-info">
        <span class="status-badge" :class="statusClass">{{ data.status }}</span>
        <div class="info-blocks-wrapper">
          <div class="info-block-group">
            <div class="info-block">
              <span class="label">Инициатор:</span>
              <span class="value">{{ data.initiatorName }}</span>
            </div>
            <div class="info-block">
              <span class="label">ID:</span>
              <span class="value">{{ data.sendingId }}</span>
            </div>
          </div>
        </div>
        <div class="info-blocks-wrapper">
          <div class="info-block-group">
            <div class="info-block">
              <span class="label">Начало:</span>
              <span class="value">{{ data.startTime }}</span>
            </div>
            <div class="info-block">
              <span class="label">Конец:</span>
              <span class="value">{{ data.endTime }}</span>
            </div>
          </div>
        </div>
      </div>
      <svg class="chevron" :class="{ rotated: isExpanded }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <polyline points="6 9 12 15 18 9"></polyline>
      </svg>
    </div>
    <Transition
      :css="false"
      @enter="enter"
      @after-enter="afterEnter"
      @leave="leave"
      @after-leave="afterLeave"
    >
      <div v-if="isExpanded" class="sending-body">
        <div class="details-list">
          <div v-for="message in data.messages" :key="message.id" class="message-card">
            <div class="message-header">
              <span class="message-id">
                Сообщение #{{ message.id }}
                <!-- <-- НОВОЕ: Визуальный индикатор ретрая -->
                <span v-if="message.messageType === MessageTypeEnum.Retry" class="retry-badge">Повторная отправка</span>
              </span>
              <span class="message-status" :class="getMessageStatusClass(message.publishingResultId)">
                {{ getPublishingResultText(message.publishingResultId) }}
              </span>
            </div>
            <div class="message-details">
              <!-- <-- НОВОЕ: Отображение подготовленных объектов только для ретрай-сообщений -->
              <div v-if="message.messageType === MessageTypeEnum.Retry" class="detail-item">
                <span class="detail-label">Подготовлено к повтору:</span>
                <span class="detail-value">{{ message.preparedObjectsCount ?? 0 }}</span>
              </div>
              
              <div class="detail-item">
                <span class="detail-label">Фактически отправлено объектов:</span>
                <span class="detail-value">{{ message.polynomObjectsAmountInMessage }}</span>
              </div>
              
              <div class="detail-item">
                <span class="detail-label">Начало сбора данных:</span>
                <span class="detail-value">{{ formatDate(message.startedCollectionFromPolynomAt) }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">Конец сбора данных:</span>
                <span class="detail-value">{{ formatDate(message.finishedCollectionFromPolynomAt) }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">Отправлено в очередь:</span>
                <span class="detail-value">{{ formatDate(message.sentAtQueue) }}</span>
              </div>
              
              <div v-if="message.messageFailure" class="failure-block">
                <div class="failure-title">Ошибка:</div>
                <div class="detail-item">
                  <span class="detail-label">Причина:</span>
                  <span class="detail-value error-text">{{ message.messageFailure.failureReasonTitle }}</span>
                </div>
                <div class="detail-item">
                  <span class="detail-label">Описание:</span>
                  <textarea
                    class="error-textarea"
                    readonly
                    :value="message.messageFailure.failureDescription"
                  ></textarea>
                </div>
                <div class="detail-item">
                  <span class="detail-label">Время ошибки:</span>
                  <span class="detail-value">{{ formatDate(message.messageFailure.failedAt) }}</span>
                </div>
              </div>
            </div>
          </div>
          <div v-if="data.messages.length === 0" class="empty-messages">
            Нет сообщений
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import {
  MessageTypeEnum,
  RabbitMqPublishingResultEnum,
  RabbitMqPublishingResultEnumDescriptions,
  SendingStatusEnum
} from '@/types/sync.types';

const props = defineProps({
  data: {
    type: Object,
    required: true
  }
});

const isExpanded = ref(false);

const statusClass = computed(() => {
  const statusId = props.data.statusId;
  return {
    'status-progress': statusId === SendingStatusEnum.Initiated || statusId === SendingStatusEnum.Pending,
    'status-done': statusId === SendingStatusEnum.Completed,
    'status-error': statusId >= SendingStatusEnum.ErrorUnknown,
    'status-unknown': statusId === SendingStatusEnum.Unknown || statusId === SendingStatusEnum.EmptySending
  };
});

const getPublishingResultText = (resultId: number) => {
  return RabbitMqPublishingResultEnumDescriptions[resultId as RabbitMqPublishingResultEnum] || 'Неизвестный результат';
};

const getMessageStatusClass = (resultId: number) => {
  if (resultId === RabbitMqPublishingResultEnum.Ack) return 'result-success';
  if (resultId === RabbitMqPublishingResultEnum.Nack ||
      resultId === RabbitMqPublishingResultEnum.TimedOut ||
      resultId === RabbitMqPublishingResultEnum.ConnectionBlocked ||
      resultId === RabbitMqPublishingResultEnum.Failed ||
      resultId === RabbitMqPublishingResultEnum.FailedDuringInformationCollection) {
    return 'result-error';
  }
  return 'result-pending';
};

const formatDate = (dateString: string | null) => {
  if (!dateString || dateString.startsWith('0001-01-01')) return '-';
  const date = new Date(dateString);
  if (isNaN(date.getTime())) return '-';
  const pad = (n: number, len: number = 2) => String(n).padStart(len, '0');
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ` +
         `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`;
};

const toggleExpand = () => {
  isExpanded.value = !isExpanded.value;
};

// ... (код startResize, afterTransition, clearInlineStyles, enter, afterEnter, leave, afterLeave остаётся без изменений) ...
const startResize = (e: MouseEvent) => {
  const textarea = (e.target as HTMLElement).previousElementSibling as HTMLTextAreaElement;
  const startY = e.clientY;
  const startHeight = textarea.offsetHeight;
  const doResize = (moveEvent: MouseEvent) => {
    const newHeight = startHeight + (moveEvent.clientY - startY);
    textarea.style.height = `${Math.max(60, newHeight)}px`;
  };
  const stopResize = () => {
    document.removeEventListener('mousemove', doResize);
    document.removeEventListener('mouseup', stopResize);
  };
  document.addEventListener('mousemove', doResize);
  document.addEventListener('mouseup', stopResize);
};

const DURATION = 320;
const EASING = 'cubic-bezier(0.33, 1, 0.68, 1)';
const prefersReducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
const afterTransition = (el: HTMLElement, property: string, duration: number, callback: () => void) => {
  let finished = false;
  const finish = () => {
    if (finished) return;
    finished = true;
    el.removeEventListener('transitionend', onEnd);
    clearTimeout(fallback);
    callback();
  };
  const onEnd = (e: TransitionEvent) => {
    if (e.target === el && e.propertyName === property) finish();
  };
  el.addEventListener('transitionend', onEnd);
  const fallback = setTimeout(finish, duration + 60);
};
const clearInlineStyles = (el: HTMLElement) => {
  el.style.transition = '';
  el.style.height = '';
  el.style.opacity = '';
  el.style.transform = '';
};
const enter = (el: Element, done: () => void) => {
  const htmlEl = el as HTMLElement;
  if (prefersReducedMotion()) { done(); return; }
  htmlEl.style.height = 'auto';
  const targetHeight = htmlEl.offsetHeight;
  htmlEl.style.transition = 'none';
  htmlEl.style.height = '0px';
  htmlEl.style.opacity = '0';
  htmlEl.style.transform = 'translateY(-6px)';
  void htmlEl.offsetHeight;
  requestAnimationFrame(() => {
    htmlEl.style.transition = `height ${DURATION}ms ${EASING}, opacity ${DURATION}ms ease-out, transform ${DURATION}ms ${EASING}`;
    htmlEl.style.height = `${targetHeight}px`;
    htmlEl.style.opacity = '1';
    htmlEl.style.transform = 'translateY(0)';
  });
  afterTransition(htmlEl, 'height', DURATION, done);
};
const afterEnter = (el: Element) => clearInlineStyles(el as HTMLElement);
const leave = (el: Element, done: () => void) => {
  const htmlEl = el as HTMLElement;
  if (prefersReducedMotion()) { done(); return; }
  htmlEl.style.transition = 'none';
  htmlEl.style.height = `${htmlEl.offsetHeight}px`;
  htmlEl.style.opacity = '1';
  htmlEl.style.transform = 'translateY(0)';
  void htmlEl.offsetHeight;
  requestAnimationFrame(() => {
    htmlEl.style.transition = `height ${DURATION}ms ${EASING}, opacity ${DURATION * 0.8}ms ease-in, transform ${DURATION}ms ${EASING}`;
    htmlEl.style.height = '0px';
    htmlEl.style.opacity = '0';
    htmlEl.style.transform = 'translateY(-6px)';
  });
  afterTransition(htmlEl, 'height', DURATION, done);
};
const afterLeave = (el: Element) => clearInlineStyles(el as HTMLElement);
</script>

<style scoped>
/* ... (все существующие стили остаются без изменений) ... */
.target-classification-node-name {
  display: flex;
  justify-content: center;
  align-items: center;
  width: fit-content;
  margin: 0 auto;
  padding: 10px 12px;
}
.target-classification-node-name-span {
  font-size: 11px;
  line-height: 1;
  color: #727272;
  white-space: nowrap;
}
.sending-card {
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 2px 10px rgba(0,0,0,0.05);
  overflow: hidden;
  transition: box-shadow 0.3s;
}
.sending-card:hover {
  box-shadow: 0 4px 15px rgba(0,0,0,0.08);
}
.sending-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 5px 20px;
  cursor: pointer;
  user-select: none;
  gap: 16px;
}
.header-info {
  display: grid;
  grid-template-columns: 35% 30% 35%;
  align-items: center;
  flex: 1;
  min-width: 0;
}
.status-badge {
  justify-self: center;
  width: 200px;
  max-width: 100%;
  padding: 6px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
  white-space: normal;
  word-wrap: break-word;
  overflow-wrap: break-word;
  text-align: center;
  line-height: 1.4;
}
.info-blocks-wrapper {
  display: flex;
  align-items: center;
  min-width: 0;
  padding-left: 16px;
  margin-top: 20px;
  margin-bottom: 20px;
  box-sizing: border-box;
}
.info-block-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
  flex-shrink: 1;
  min-width: 0;
}
.info-block {
  display: flex;
  flex-direction: column;
  flex-shrink: 1;
  min-width: 0;
}
.label {
  font-size: 11px;
  color: #999;
  margin-bottom: 2px;
}
.value {
  font-size: 14px;
  font-weight: 500;
  color: #333;
  word-break: break-word;
}
.status-progress { background-color: #fff3cd; color: #856404; }
.status-done { background-color: #d4edda; color: #155724; }
.status-error { background-color: #f8d7da; color: #721c24; }
.status-unknown { background-color: #e2e3e5; color: #383d41; }
.chevron {
  width: 24px;
  height: 24px;
  color: #888;
  transition: transform 320ms cubic-bezier(0.33, 1, 0.68, 1);
  flex-shrink: 0;
}
.chevron.rotated {
  transform: rotate(180deg);
}
.sending-body {
  overflow: hidden;
  will-change: height, opacity, transform;
}
.details-list {
  padding: 0 20px 16px;
  border-top: 1px solid #f0f0f0;
  padding-top: 16px;
}
.message-card {
  background: #f9f9f9;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 12px;
  border: 1px solid #e0e0e0;
}
.message-card:last-child {
  margin-bottom: 0;
}
.message-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  padding-bottom: 8px;
  border-bottom: 1px solid #e0e0e0;
}
.message-id {
  font-weight: 600;
  font-size: 14px;
  color: #333;
  display: flex;
  align-items: center;
  gap: 8px; /* Отступ между ID и бейджем ретрая */
}
/* <-- НОВЫЙ СТИЛЬ: Бейдж для ретрай-сообщений */
.retry-badge {
  display: inline-block;
  font-size: 10px;
  font-weight: 700;
  color: #d35400;
  background-color: #fdebd0;
  border: 1px solid #f5cba7;
  border-radius: 4px;
  padding: 2px 6px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.message-status {
  padding: 3px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 500;
}
.result-success { background-color: #d4edda; color: #155724; }
.result-error { background-color: #f8d7da; color: #721c24; }
.result-pending { background-color: #fff3cd; color: #856404; }
.message-details {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.detail-item {
  display: flex;
  justify-content: space-between;
  font-size: 13px; 
}
.detail-label {
  color: #666;
  font-weight: 500;
}
.detail-value {
  color: #333;
  text-align: right;
  max-width: 80%;
  word-break: break-word;
}
.failure-block {
  margin-top: 10px;
  padding: 10px;
  background: #fff5f5;
  border-radius: 6px;
  border-left: 3px solid #dc3545;
}
.failure-title {
  font-weight: 600;
  color: #dc3545;
  margin-bottom: 6px;
  font-size: 13px;
}
.error-textarea {
  width: 60%;
  min-height: 40px;
  max-height: 300px;
  padding: 6px 8px;
  border-radius: 4px;
  background: #fff5f5;
  color: #333;
  font-family: inherit;
  font-size: 13px;
  text-align: left;
  resize: vertical;
  overflow: auto;
  box-sizing: border-box;
  line-height: 1.4;
  cursor: text;
}
.empty-messages {
  text-align: center;
  padding: 20px;
  color: #999;
  font-style: italic;
}
</style>