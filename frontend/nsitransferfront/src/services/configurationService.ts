import { apiFetch } from './httpClient';
import { API_CONFIG } from '@/config/apiConfig';
import { handleApiError } from '@/services/apiErrorHandling';

export interface TargetReferenceNodeConfig {
  targetReferenceNodeObjectId: number;
  targetReferenceNodeTypeId: number;
  targetReferenceNodeName: string;
}

export const getTargetReferenceNode = async (): Promise<TargetReferenceNodeConfig> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.CONFIGURATION}/target-reference-node`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки конфигурации целевого справочника');
  return response.json();
};

export const updateTargetReferenceNode = async (data: TargetReferenceNodeConfig): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.CONFIGURATION}/target-reference-node`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения целевого справочника');
};

// Точная структура ответа сервера для узла классификации
export interface ClassificationNodeObject {
  writeAccess: boolean;
  objectId: number;
  typeId: number;
}

export interface ClassificationTreeNode {
  id: string | null;
  applicability: number;
  hasObjects: boolean;
  isSystemObject: boolean;
  iconCode: number;
  iconColor: string | null;
  leaf: boolean;
  count: number;
  location: string | null;
  parent: ClassificationNodeObject;
  nodeObject: ClassificationNodeObject;
  filterConditionApply: boolean;
  writeAccess: boolean;
  accessRight: number;
  name: string;
  objectId: number;
  typeId: number;
}

export const getFirstLayerReferences = async (): Promise<ClassificationTreeNode[]> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.ADMIN_PANEL}/reference/first-layer`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки списка справочников');
  return response.json();
};


// ====== RabbitMQ ======

export interface RabbitMqQueues {
  nsiTransferExchangeName: string;
  polynomSearchResultsQueueName: string;
}

export interface RabbitMqRetryParams {
  maxRetryAttempts: number;
  retryIntervalInSeconds: number;
  confirmationTimeOutInSeconds: number;
}

export const getRabbitMqQueues = async (): Promise<RabbitMqQueues> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.RABBITMQ_QUEUES}`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки конфигурации очередей RabbitMQ');
  return response.json();
};

export const updateRabbitMqQueues = async (data: RabbitMqQueues): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.RABBITMQ_QUEUES}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения конфигурации очередей RabbitMQ');
};

export const getRabbitMqRetryParams = async (): Promise<RabbitMqRetryParams> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.RABBITMQ_RETRY_PARAMS}`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки параметров повторов RabbitMQ');
  return response.json();
};

export const updateRabbitMqRetryParams = async (data: RabbitMqRetryParams): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.RABBITMQ_RETRY_PARAMS}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения параметров повторов RabbitMQ');
};


// ====== Polynom ======

export interface PolynomConfig {
  address: string;
  dbName: string;
  timeZoneId: string;
}

export interface PolynomApiSyncOptions {
  startSyncWithIntervalMinutes: number;
  conceptNameForClassificationData: string;
  classificationCodePropertyName: string;
  ownContractName: string;
  minCodePropertyName: string;
  maxCodePropertyName: string;
}

export const getPolynomConfig = async (): Promise<PolynomConfig> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.POLYNOM_CONFIG}`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки конфигурации Polynom');
  return response.json();
};

export const updatePolynomConfig = async (data: PolynomConfig): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.POLYNOM_CONFIG}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения конфигурации Polynom');
};

export const getPolynomApiSyncOptions = async (): Promise<PolynomApiSyncOptions> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.POLYNOM_API_SYNC_OPTIONS}`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки опций синхронизации Polynom');
  return response.json();
};

export const updatePolynomApiSyncOptions = async (data: PolynomApiSyncOptions): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.POLYNOM_API_SYNC_OPTIONS}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения опций синхронизации Polynom');
};


// ====== Email ======

export interface EmailNotificationsOptions {
  errorRecipients: string[];
}

export const getEmailNotifications = async (): Promise<EmailNotificationsOptions> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.EMAIL_NOTIFICATIONS}`);
  if (!response.ok) await handleApiError(response, 'Ошибка загрузки настроек email-уведомлений');
  return response.json();
};

export const updateEmailNotifications = async (data: EmailNotificationsOptions): Promise<void> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.EMAIL_NOTIFICATIONS}`, {
    method: 'PUT',
    body: JSON.stringify(data)
  });
  if (!response.ok) await handleApiError(response, 'Ошибка сохранения настроек email-уведомлений');
};