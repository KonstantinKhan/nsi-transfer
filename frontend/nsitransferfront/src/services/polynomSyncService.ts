import { API_CONFIG } from '@/config/apiConfig';
import { 
  type MessageEmptyPayload, 
  type AllSyncEventHandlers, 
  type ErrorPayload, 
  type MessageCreatedPayload, 
  type MessageFailedPayload, 
  type MessagePublishedPayload, 
  type ObjectsCollectedPayload, 
  type SendingModel, 
  type SendingSyncEventEnvelope, 
  type StatusChangedPayload, 
  type SendingCompletedPayload,
  type RetryMessageCreatedPayload, type RetryObjectsCollectedPayload,
  type RetryMessagePublishedPayload, type RetryMessageEmptyPayload,
  type GetSendingsParams } from '@/types/sync.types';
import { apiFetch } from './httpClient';
import { refreshToken } from './authService';
import { getCurrentUserNameOrLogin } from './authStorage';
import { handleApiError } from '@/services/apiErrorHandling';


/**
 * Получить все синхронизации
 */
export const getSendings = async (params: GetSendingsParams): Promise<{ items: SendingModel[], hasMore: boolean }> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.GET_SENDINGS}?pageSize=${params.pageSize || 10}&cursorInitiatedAt=${encodeURIComponent(params.cursorInitiatedAt || '')}&cursorId=${params.cursorId || ''}`);

  if (!response.ok) {
    await handleApiError(response, 'Ошибка загрузки списка отправлений');
  }

  return response.json() as Promise<{ items: SendingModel[], hasMore: boolean }>;
};


export const getSending = async(sendingId: string): Promise<SendingModel> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.GET_SENDING}?sendingId=${sendingId}`)

  if (!response.ok) {
    await handleApiError(response, `Ошибка загрузки отправления с sendingId ${sendingId}`)
  }

  return response.json() as Promise<SendingModel>;
}


/**
 * Запустить синхронизацию в фоновом режиме
 */
export const startSync = async (): Promise<SendingModel> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.START_SYNC}`, {
    method: 'POST',
    body: JSON.stringify({ initiatorName: getCurrentUserNameOrLogin() })
  });

  if (!response.ok) {
    await handleApiError(response, 'Ошибка сервера при запуске синхронизации');
  }

  return response.json() as Promise<SendingModel>;
};


/**
 * Запустить полную переиндексацию персистентного кеша последних кодов классификатора
 * по всем группам целевого справочника. Долгая фоновая операция — эндпоинт отвечает сразу,
 * результат (сколько групп проиндексировано/пропущено/с ошибками) смотреть в логах сервера.
 * Отказывает (409), если сейчас выполняется синхронизация.
 */
export const rebuildGroupCodeCache = async (): Promise<{ message: string }> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.REBUILD_GROUP_CODE_CACHE}`, {
    method: 'POST'
  });

  if (!response.ok) {
    await handleApiError(response, 'Ошибка запуска переиндексации кеша кодов классификатора');
  }

  return response.json() as Promise<{ message: string }>;
};


export const listenForAllSyncEvents = (
  handlers: AllSyncEventHandlers
): { close: () => void; isUnauthorized: () => boolean } => {
  const url = `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.LISTEN_ALL_SYNC_EVENTS}`;
  let eventSource: EventSource | null = null;
  let everConnected = false;
  let recoveryAttempted = false;
  let unauthorized = false;
  let closedByUser = false;

  const parseEnvelope = <T>(data: string): SendingSyncEventEnvelope<T> | null => {
    try {
      return JSON.parse(data);
    } catch {
      console.warn(`[SSE] Не удалось распарсить data:`, data);
      return null;
    }
  };

  const attachListeners = (source: EventSource) => {
    // --- Подписка на реальные события сервера ---
    source.addEventListener('StatusChanged', (e) => {
      const envelope = parseEnvelope<StatusChangedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onStatusChanged?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('MessageCreated', (e) => {
      const envelope = parseEnvelope<MessageCreatedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onMessageCreated?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('ObjectsCollected', (e) => {
      const envelope = parseEnvelope<ObjectsCollectedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onObjectsCollected?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('MessageEmpty', (e) => {
      const envelope = parseEnvelope<MessageEmptyPayload>((e as MessageEvent).data);
      if (envelope) handlers.onMessageEmpty?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('MessagePublished', (e) => {
      const envelope = parseEnvelope<MessagePublishedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onMessagePublished?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('MessageFailed', (e) => {
      const envelope = parseEnvelope<MessageFailedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onMessageFailed?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('SendingCompleted', (e) => {
      const envelope = parseEnvelope<SendingCompletedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onSendingCompleted?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('Error', (e) => {
      const envelope = parseEnvelope<ErrorPayload>((e as MessageEvent).data);
      if (envelope) handlers.onError?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('Closed', (e) => {
      const envelope = parseEnvelope<null>((e as MessageEvent).data);
      if (envelope) handlers.onClosed?.(envelope.sendingId);
    });

    // <-- НОВЫЕ: Подписка на события Retry
    source.addEventListener('RetryMessageCreated', (e) => {
      const envelope = parseEnvelope<RetryMessageCreatedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onRetryMessageCreated?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('RetryObjectsCollected', (e) => {
      const envelope = parseEnvelope<RetryObjectsCollectedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onRetryObjectsCollected?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('RetryMessagePublished', (e) => {
      const envelope = parseEnvelope<RetryMessagePublishedPayload>((e as MessageEvent).data);
      if (envelope) handlers.onRetryMessagePublished?.(envelope.sendingId, envelope.payload);
    });
    source.addEventListener('RetryMessageEmpty', (e) => {
      const envelope = parseEnvelope<RetryMessageEmptyPayload>((e as MessageEvent).data);
      if (envelope) handlers.onRetryMessageEmpty?.(envelope.sendingId, envelope.payload);
    });
  };

  const attemptRecovery = async () => {
    const newAuth = await refreshToken();
    if (closedByUser) return;
    if (!newAuth) {
      unauthorized = true;
      console.error('[SSE] Refresh токена не удался, соединение закрыто.');
      handlers.onConnectionError?.(new Event('error'));
      return;
    }
    open();
  };

  const open = () => {
    recoveryAttempted = false;
    eventSource = new EventSource(url, { withCredentials: true });
    attachListeners(eventSource);
    eventSource.onopen = () => {
      everConnected = true;
    };
    eventSource.onerror = (e) => {
      if (closedByUser) return;
      if (eventSource?.readyState === EventSource.CONNECTING) {
        handlers.onConnectionError?.(e);
        return;
      }
      eventSource?.close();
      eventSource = null;
      if (!everConnected) {
        unauthorized = true;
        console.error('[SSE] Соединение отклонено (вероятно 401), повторные попытки отключены.');
        handlers.onConnectionError?.(e);
        return;
      }
      if (recoveryAttempted) {
        unauthorized = true;
        console.error('[SSE] Попытка восстановления уже исчерпана, соединение закрыто.');
        handlers.onConnectionError?.(e);
        return;
      }
      recoveryAttempted = true;
      void attemptRecovery();
      handlers.onConnectionError?.(e);
    };
  };

  open();

  return {
    close: () => {
      closedByUser = true;
      eventSource?.close();
      eventSource = null;
    },
    isUnauthorized: () => unauthorized
  };
};