let baseUrl = 'http://localhost:8080/api'

try {
  const config = (window as any).__APP_CONFIG__
  if (config?.API_BASE_URL) {
    baseUrl = config.API_BASE_URL
  }
} catch {
  // fallback to default
}

export const API_CONFIG = {
  BASE_URL: baseUrl,
  ENDPOINTS: {
    START_SYNC: '/polynom-sync/start-data-collection-in-background',
    REBUILD_GROUP_CODE_CACHE: '/polynom-sync/rebuild-group-code-cache',
    GET_SENDINGS: '/polynom-sync/sendings',
    GET_SENDING: '/polynom-sync/sending',
    LISTEN_EVENTS: '/polynom-sync/listen-for-sync-events',
    LISTEN_ALL_SYNC_EVENTS: '/polynom-sync/listen-for-all-sync-events',

    STORAGES: '/auth/storages',
    AUTH: '/auth/signin',
    SIGNOUT: '/auth/signout',
    REFRESH_TOKEN: '/auth/refresh-token',

    CONFIGURATION: '/configuration',
    ADMIN_PANEL: '/admin-panel',

    RABBITMQ_QUEUES: '/configuration/rabbitmq-queues',
    RABBITMQ_RETRY_PARAMS: '/configuration/rabbitmq-retry-params',

    POLYNOM_CONFIG: '/configuration/polynom-config',
    POLYNOM_API_SYNC_OPTIONS: '/configuration/polynom-api-sync-options',

    EMAIL_NOTIFICATIONS: '/configuration/email-notifications'
  }
};