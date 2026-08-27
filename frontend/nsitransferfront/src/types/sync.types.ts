export enum SendingStatusEnum {
  Unknown = 0,
  Initiated = 1,
  Pending = 2,
  Completed = 3,
  EmptySending = 4,
  ErrorUnknown = 100,
  ErrorOccuredWhilePreparing = 101,
  ErrorOccuredWhileCollectingDataForMessage = 102,
  ErrorOccuredWhilePublishingMessage = 103
}

export const SendingStatusEnumDescriptions: Record<SendingStatusEnum, string> = {
  [SendingStatusEnum.Unknown]: "Неизвестный статус, отсутствие статуса",
  [SendingStatusEnum.Initiated]: "Инициировано начало отправления",
  [SendingStatusEnum.Pending]: "В процессе сборки и отправления сообщений",
  [SendingStatusEnum.Completed]: "Все сообщения из отправления успешно собраны и отправлены в брокер сообщений",
  [SendingStatusEnum.EmptySending]: "Сформировано пустое сообщение. Новых изменений в системе Полином не было найдено",
  [SendingStatusEnum.ErrorUnknown]: "Неизвестная ошибка",
  [SendingStatusEnum.ErrorOccuredWhilePreparing]: "Ошибка при подготовке отправления",
  [SendingStatusEnum.ErrorOccuredWhileCollectingDataForMessage]: "Ошибка при сборе данных для одного из сообщений в отправлении",
  [SendingStatusEnum.ErrorOccuredWhilePublishingMessage]: "Ошибка при публикации одного из сообщений в отправлении"
};

export enum RabbitMqPublishingResultEnum {
  Unknown = 0,
  Ack = 1,
  Nack = 2,
  TimedOut = 3,
  ConnectionBlocked = 4,
  Failed = 5,
  FailedDuringInformationCollection = 6
}

export const RabbitMqPublishingResultEnumDescriptions: Record<RabbitMqPublishingResultEnum, string> = {
  [RabbitMqPublishingResultEnum.Unknown]: "Сборка сообщения",
  [RabbitMqPublishingResultEnum.Ack]: "Подтверждение от RabbitMQ",
  [RabbitMqPublishingResultEnum.Nack]: "Отрицательное подтверждение от RabbitMQ",
  [RabbitMqPublishingResultEnum.TimedOut]: "Превышено время ожидания подтверждения от RabbitMQ",
  [RabbitMqPublishingResultEnum.ConnectionBlocked]: "Подключение к RabbitMQ было заблокировано",
  [RabbitMqPublishingResultEnum.Failed]: "Не удалось подключиться к RabbitMQ или опубликовать сообщение в очередь RabbitMQ после нескольких попыток",
  [RabbitMqPublishingResultEnum.FailedDuringInformationCollection]: "Не удалось собрать объекты со свойствами перед формированием самого сообщения для отправки"
};

export enum MessageTypeEnum {
  FirstSend = 1,
  Retry = 2
}

export interface MessageFailureModel {
  messageId: number;
  failedAt: string;
  failureDescription: string;
  failureReasonTitle: string;
}

export interface MessageModel {
  id: number;
  startedCollectionFromPolynomAt: string;
  finishedCollectionFromPolynomAt: string | null;
  sentAtQueue: string | null;
  polynomObjectsAmountInMessage: number; // Фактически отправлено
  preparedObjectsCount?: number; // <-- НОВОЕ: общее число подготовленных к отправке (для ретрая)
  publishingResultId: RabbitMqPublishingResultEnum;
  messageFailure: MessageFailureModel | null;
  messageType: MessageTypeEnum;
}

export interface GetSendingsParams {
  pageSize?: number;
  cursorInitiatedAt?: string;
  cursorId?: string;
}

export interface SendingModel {
  id: string;
  initiatedAt: string;
  endedAt: string | null;
  initiatorName: string;
  statusId: SendingStatusEnum;
  targetReferenceNodeName: string;
  messages: MessageModel[];
}

// --- Существующие Payload ---
export interface StatusChangedPayload {
  status: string;
  endedAt?: string;
  error?: string;
  sendingModel?: SendingModel;
}
export interface MessageCreatedPayload {
  id: number;
  startedCollectionFromPolynomAt: string;
  pageNumber: number;
}
export interface ObjectsCollectedPayload {
  id: number;
  objectsCount: number;
  finishedCollectionFromPolynomAt: string;
}
export interface MessageEmptyPayload {
  id: number;
}
export interface MessagePublishedPayload {
  id: number;
  sentAtQueue: string;
}
export interface MessageFailedPayload {
  id: number;
  error: string;
  errorStatus: string;
}
export interface SendingCompletedPayload {
  endedAt: string;
}
export interface ErrorPayload {
  message: string;
}

// --- НОВЫЕ Payload для Retry событий ---
export interface RetryMessageCreatedPayload {
  id: number;
  objectsToRetryCount: number;
}
export interface RetryObjectsCollectedPayload {
  id: number;
  objectsCount: number;
  finishedCollectionFromPolynomAt: string;
}
export interface RetryMessagePublishedPayload {
  id: number;
  sentAtQueue: string;
}
export interface RetryMessageEmptyPayload {
  id: number;
  finishedCollectionFromPolynomAt: string;
}

export interface SendingSyncEventEnvelope<TPayload = unknown> {
  sendingId: string;
  eventType: string;
  payload: TPayload;
}

export interface AllSyncEventHandlers {
  onStatusChanged?: (sendingId: string, payload: StatusChangedPayload) => void;
  onMessageCreated?: (sendingId: string, payload: MessageCreatedPayload) => void;
  onObjectsCollected?: (sendingId: string, payload: ObjectsCollectedPayload) => void;
  onMessageEmpty?: (sendingId: string, payload: MessageEmptyPayload) => void;
  onMessagePublished?: (sendingId: string, payload: MessagePublishedPayload) => void;
  onMessageFailed?: (sendingId: string, payload: MessageFailedPayload) => void;
  onSendingCompleted?: (sendingId: string, payload: SendingCompletedPayload) => void;
  onError?: (sendingId: string, payload: ErrorPayload) => void;
  onClosed?: (sendingId: string) => void;
  onConnectionError?: (e: Event) => void;
  
  // <-- НОВЫЕ обработчики
  onRetryMessageCreated?: (sendingId: string, payload: RetryMessageCreatedPayload) => void;
  onRetryObjectsCollected?: (sendingId: string, payload: RetryObjectsCollectedPayload) => void;
  onRetryMessagePublished?: (sendingId: string, payload: RetryMessagePublishedPayload) => void;
  onRetryMessageEmpty?: (sendingId: string, payload: RetryMessageEmptyPayload) => void;
}