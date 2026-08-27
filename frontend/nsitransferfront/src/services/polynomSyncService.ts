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


export const listenForAllSyncEvents = (
  handlers: AllSyncEventHandlers
): EventSource => {
  const url = `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.LISTEN_ALL_SYNC_EVENTS}`;
  const eventSource = new EventSource(url, { withCredentials: true });

  const parseEnvelope = <T>(data: string): SendingSyncEventEnvelope<T> | null => {
    try {
      return JSON.parse(data);
    } catch {
      console.warn(`[SSE] Не удалось распарсить data:`, data);
      return null;
    }
  };

  // --- Подписка на реальные события сервера ---
  eventSource.addEventListener('StatusChanged', (e) => {
    const envelope = parseEnvelope<StatusChangedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onStatusChanged?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('MessageCreated', (e) => {
    const envelope = parseEnvelope<MessageCreatedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onMessageCreated?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('ObjectsCollected', (e) => {
    const envelope = parseEnvelope<ObjectsCollectedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onObjectsCollected?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('MessageEmpty', (e) => {
    const envelope = parseEnvelope<MessageEmptyPayload>((e as MessageEvent).data);
    if (envelope) handlers.onMessageEmpty?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('MessagePublished', (e) => {
    const envelope = parseEnvelope<MessagePublishedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onMessagePublished?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('MessageFailed', (e) => {
    const envelope = parseEnvelope<MessageFailedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onMessageFailed?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('SendingCompleted', (e) => {
    const envelope = parseEnvelope<SendingCompletedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onSendingCompleted?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('Error', (e) => {
    const envelope = parseEnvelope<ErrorPayload>((e as MessageEvent).data);
    if (envelope) handlers.onError?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('Closed', (e) => {
    const envelope = parseEnvelope<null>((e as MessageEvent).data);
    if (envelope) handlers.onClosed?.(envelope.sendingId);
  });

  // <-- НОВЫЕ: Подписка на события Retry
  eventSource.addEventListener('RetryMessageCreated', (e) => {
    const envelope = parseEnvelope<RetryMessageCreatedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onRetryMessageCreated?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('RetryObjectsCollected', (e) => {
    const envelope = parseEnvelope<RetryObjectsCollectedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onRetryObjectsCollected?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('RetryMessagePublished', (e) => {
    const envelope = parseEnvelope<RetryMessagePublishedPayload>((e as MessageEvent).data);
    if (envelope) handlers.onRetryMessagePublished?.(envelope.sendingId, envelope.payload);
  });
  eventSource.addEventListener('RetryMessageEmpty', (e) => {
    const envelope = parseEnvelope<RetryMessageEmptyPayload>((e as MessageEvent).data);
    if (envelope) handlers.onRetryMessageEmpty?.(envelope.sendingId, envelope.payload);
  });

  // --- Обработка ошибок соединения ---
  eventSource.onerror = (e) => {
    handlers.onConnectionError?.(e);
    if (eventSource.readyState === EventSource.CLOSED) {
      eventSource.close();
    }
  };

  return eventSource;
};