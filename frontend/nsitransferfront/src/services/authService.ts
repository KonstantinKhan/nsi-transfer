import { apiFetch } from './httpClient';
import { API_CONFIG } from '@/config/apiConfig';
import { saveSessionMetadata, clearSessionMetadata } from './authStorage';
import type { SignInRequest, AuthResponse } from '@/types/auth.types';

// Расширяем AuthResponse метаданными сессии
interface AuthResponseWithMeta extends AuthResponse {
  isAuthenticated: boolean;
  expiresAt: number;
  firstName: string;
  lastName: string;
  patronymic: string;
  login: string;
  email: string;
}

// export const getStorages = async (): Promise<StorageDefinition[]> => {
//   const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.STORAGES}`);
//   if (!response.ok) throw new Error('Ошибка загрузки хранилищ');
//   const data = await response.json();
//   return data.storages;
// };

export const signIn = async (req: SignInRequest): Promise<AuthResponseWithMeta> => {
  const response = await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.AUTH}`, {
    method: 'POST',
    body: JSON.stringify(req)
  });

  if (!response.ok) {
    const err = await response.json().catch(() => ({ error: 'Ошибка авторизации' }));
    throw new Error(err.error || err.detail || 'Ошибка авторизации');
  }

  const authData = await response.json() as AuthResponseWithMeta;
  
  // Сохраняем метаданные сессии (не токены!)
  saveSessionMetadata({
    isAuthenticated: authData.isAuthenticated,
    expiresAt: authData.expiresAt,
    firstName: authData.firstName,
    lastName: authData.lastName,
    patronymic: authData.patronymic,
    login: authData.login,
    email: authData.email
  });
  
  return authData;
};

export const signOut = async (): Promise<void> => {
  try {
    await apiFetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.SIGNOUT}`, {
      method: 'DELETE'
    });
  } finally {
    // Очищаем локальные метаданные (сервер сам очистит HttpOnly cookies)
    clearSessionMetadata();
  }
};

export const refreshToken = async (): Promise<AuthResponseWithMeta | null> => {
  const response = await fetch(`${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.REFRESH_TOKEN}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({})
  });

  if (!response.ok) {
    clearSessionMetadata();
    return null;
  }

  const authData = await response.json() as AuthResponseWithMeta;
  
  // Обновляем метаданные сессии
  saveSessionMetadata({
    isAuthenticated: authData.isAuthenticated,
    expiresAt: authData.expiresAt,
    firstName: authData.firstName,
    lastName: authData.lastName,
    patronymic: authData.patronymic,
    login: authData.login,
    email: authData.email
  });
  
  return authData;
};