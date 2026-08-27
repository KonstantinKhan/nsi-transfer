import { refreshToken } from './authService';
import { isAuthenticated } from './authStorage';

let isRefreshing = false;
let refreshSubscribers: ((token: string) => void)[] = [];

const onRefreshed = (token: string) => {
  refreshSubscribers.forEach(callback => callback(token));
  refreshSubscribers = [];
};

const addRefreshSubscriber = (callback: (token: string) => void) => {
  refreshSubscribers.push(callback);
};


export const apiFetch = async (url: string, options: RequestInit = {}): Promise<Response> => {
  const config: RequestInit = {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  };

  let response = await fetch(url, config);

  // Если токен истёк (401), пытаемся обновить
  if (response.status === 401 && isAuthenticated()) {
    if (!isRefreshing) {
      isRefreshing = true;
      
      try {
        const newAuth = await refreshToken();
        if (newAuth) {
          // Повторяем исходный запрос с новыми cookies
          response = await fetch(url, config);
          isRefreshing = false;
          onRefreshed(''); // Токен в cookie, передаём пустую строку
        } else {
          isRefreshing = false;
          refreshSubscribers = [];
        }
      } catch (error) {
        isRefreshing = false;
        refreshSubscribers = [];
        throw error;
      }
    } else {
      // Если уже идёт обновление, ждём и повторяем запрос
      return new Promise((resolve) => {
        addRefreshSubscriber(() => {
          resolve(fetch(url, config));
        });
      });
    }
  }

  return response;
};