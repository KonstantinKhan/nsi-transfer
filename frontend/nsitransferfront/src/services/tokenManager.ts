import { getExpiresAt } from './authStorage';
import { refreshToken } from './authService';

let refreshTimeoutId: number | null = null;

export const scheduleTokenRefresh = () => {
  if (refreshTimeoutId) {
    clearTimeout(refreshTimeoutId);
  }

  const expiresAt = getExpiresAt();
  if (!expiresAt) return;

  const now = Date.now();
  // Обновляем за 60 секунд до истечения срока действия
  const delay = Math.max(expiresAt - now - 60000, 5000); // Минимум 5 сек задержки

  refreshTimeoutId = window.setTimeout(async () => {
    console.log('Автоматическое обновление токена...');
    const newAuth = await refreshToken();
    if (newAuth) {
      scheduleTokenRefresh(); // Планируем следующее обновление
    } else {
      console.error('Не удалось обновить токен. Необходим повторный вход.');
      window.location.href = '/login';
    }
  }, delay);
};

// Инициализация при старте приложения
export const initTokenManager = () => {
  if (getExpiresAt()) {
    scheduleTokenRefresh();
  }
};