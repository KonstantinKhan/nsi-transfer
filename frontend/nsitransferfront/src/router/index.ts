import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '@/views/LoginView.vue'
import MainView from '@/views/MainView.vue'

import { refreshToken } from '@/services/authService'
import { scheduleTokenRefresh } from '@/services/tokenManager'
import {
  isAuthenticated,
  isAccessTokenAlive,
  clearSessionMetadata
} from '@/services/authStorage'

const routes = [
  {
    path: '/',
    redirect: '/login'
  },
  {
    path: '/login',
    name: 'login',
    component: LoginView
  },
  {
    path: '/main',
    component: MainView,
    children: [
      {
        path: '',
        name: 'sync',
        component: () => import('@/views/SyncView.vue')
      },
      {
        path: 'config',
        component: () => import('@/views/ConfigurationView.vue'!),
        children: [
          { path: '', name: 'config-menu', component: () => import('@/views/ConfigurationView.vue'!) },
          { path: 'target-node', name: 'config-target-node', component: () => import('@/views/config/TargetNodeView.vue') },
          { path: 'rabbitmq', name: 'config-rabbitmq', component: () => import('@/views/config/RabbitMqView.vue'!) },
          { path: 'polynom', name: 'config-polynom', component: () => import('@/views/config/PolynomView.vue'!) },
          { path: 'email', name: 'config-email', component: () => import('@/views/config/EmailView.vue'!) }
        ]
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
})

/**
 * Пытается обновить access token через refresh token.
 * Возвращает true, если обновление прошло успешно.
 */
const tryRefreshAccessToken = async (): Promise<boolean> => {
  try {
    const newAuthData = await refreshToken();
    if (newAuthData) {
      scheduleTokenRefresh();
      return true;
    }
  } catch (error) {
    console.warn('Не удалось обновить access token:', error);
  }
  return false;
};

// Глобальный перехватчик маршрутов
router.beforeEach(async (to, from, next) => {
  const isGoingToLogin = to.path === '/login';
  const isGoingToMain = to.path.startsWith('/main');

  // === Сценарий 1: Пользователь переходит на /login ===
  if (isGoingToLogin) {
    // Если сессия живая — незачем сидеть на логине, редиректим на главную
    if (isAuthenticated() && isAccessTokenAlive()) {
      scheduleTokenRefresh();
      return next('/main');
    }

    // Сессия есть, но access token истёк — пробуем обновить
    if (isAuthenticated() && !isAccessTokenAlive()) {
      const refreshed = await tryRefreshAccessToken();
      if (refreshed) {
        return next('/main');
      }
      // Не удалось обновить — очищаем метаданные и пускаем на логин
      clearSessionMetadata();
      return next();
    }

    // Сессии нет — просто пускаем на страницу логина
    return next();
  }

  // === Сценарий 2: Пользователь переходит на защищённую страницу ===
  if (isGoingToMain) {
    // Если сессия живая и access token не истёк — пускаем дальше
    if (isAuthenticated() && isAccessTokenAlive()) {
      scheduleTokenRefresh();
      return next();
    }

    // Access token истёк или сессии нет — пробуем обновить через refresh token
    const refreshed = await tryRefreshAccessToken();
    if (refreshed) {
      return next();
    }

    // Обновить не удалось — отправляем на логин
    clearSessionMetadata();
    return next('/login');
  }

  // === Сценарий 3: Все остальные маршруты ===
  return next();
});

export default router