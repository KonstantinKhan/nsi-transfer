/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * Базовый URL API NsiTransfer, как его видит браузер.
   * Примеры: http://192.168.1.50:8080/api или /api (если раздаётся через тот же nginx).
   * Подставляется в Docker при сборке образа (build arg VITE_API_BASE_URL).
   */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
