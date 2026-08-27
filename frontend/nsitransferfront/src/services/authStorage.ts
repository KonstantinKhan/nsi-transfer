/**
 * Модуль для работы с метаданными сессии.
 * 
 * ВАЖНО: Сами токены (access/refresh) хранятся в HttpOnly cookies
 * и недоступны из JavaScript. Здесь мы храним только метаданные
 * сессии — флаг авторизации и время истечения access token.
 * Эти данные не являются секретами и нужны только для UI-логики.
 */

const SESSION_KEY = 'nsi_auth_session';

export interface SessionMetadata {
  isAuthenticated: boolean;
  expiresAt: number; // Unix timestamp в миллисекундах
  firstName: string;
  lastName: string;
  patronymic: string;
  login: string;
  email: string;
}

/**
 * Сохраняет метаданные сессии после успешного логина/рефреша
 */
export const saveSessionMetadata = (data: SessionMetadata) => {
  localStorage.setItem(SESSION_KEY, JSON.stringify(data));
};

/**
 * Получает метаданные сессии
 */
export const getSessionMetadata = (): SessionMetadata | null => {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SessionMetadata;
  } catch {
    return null;
  }
};

/**
 * Проверяет, авторизован ли пользователь (по локальным метаданным).
 * 
 * Это НЕ проверка валидности токена — это проверка того,
 * что клиент успешно прошёл аутентификацию и сессия не истекла.
 * Реальная валидация происходит на сервере при каждом запросе.
 */
export const isAuthenticated = (): boolean => {
  const metadata = getSessionMetadata();
  if (!metadata) return false;
  return metadata.isAuthenticated;
};

/**
 * Проверяет, жив ли access token по времени истечения.
 * Добавляем буфер 30 секунд, чтобы не попасть в ситуацию,
 * когда токен формально жив, но уже истекает.
 */
export const isAccessTokenAlive = (): boolean => {
  const metadata = getSessionMetadata();
  if (!metadata) return false;
  const bufferMs = 30 * 1000;
  return Date.now() + bufferMs < metadata.expiresAt;
};

/**
 * Получает время истечения access token
 */
export const getExpiresAt = (): number | null => {
  const metadata = getSessionMetadata();
  return metadata?.expiresAt ?? null;
};

/**
 * Очищает метаданные сессии (при signout или ошибке refresh)
 */
export const clearSessionMetadata = () => {
  localStorage.removeItem(SESSION_KEY);
};


export const getCurrentUserNameOrLogin = (): string => {
  const metadata = getSessionMetadata();

  if (!metadata) {
    return 'Неизвестный пользователь веб-клиента';
  }

  const { firstName, lastName, patronymic, login, email } = metadata;

  const isFilled = (str?: string): boolean => Boolean(str && str.trim().length > 0);

  if (isFilled(firstName) && isFilled(lastName) && isFilled(patronymic)) {
    return `${firstName} ${patronymic} ${lastName}`.trim();
  }

  if (isFilled(firstName) && isFilled(lastName)) {
    return `${firstName} ${lastName}`.trim();
  }

  if (isFilled(email)) {
    return `${email} (${login})`.trim();
  }

  return login;
};