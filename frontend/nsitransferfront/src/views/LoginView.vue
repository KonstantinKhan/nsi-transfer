<template>
  <div class="login-page">
    <HeaderBar />
    
    <div class="login-content">
      <form class="login-form" @submit.prevent="handleLogin">
        <h2 class="form-title">Авторизация</h2>
        
        <!-- Выпадающий список хранилищ -->
        <!-- <div class="input-group">
          <span class="input-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <ellipse cx="12" cy="5" rx="9" ry="3"></ellipse>
              <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path>
              <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"></path>
            </svg>
          </span>
          <select 
            v-model="selectedStorage" 
            class="login-input login-select" 
            :disabled="isLoadingStorages"
            required
          >
            <option value="" disabled>{{ isLoadingStorages ? 'Загрузка хранилищ...' : 'Выберите хранилище' }}</option>
            <option v-for="storage in storages" :key="storage.storageId" :value="storage.storageId">
              {{ storage.displayName }}
            </option>
          </select>
        </div> -->

        <!-- Логин field -->
        <div class="input-group">
          <span class="input-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
          </span>
          <input v-model="login" type="text" placeholder="Логин" class="login-input" @keydown="onKeydown" required />
        </div>

        <!-- Пароль field -->
        <div class="input-group">
          <span class="input-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          </span>
          <input 
            v-model="password" 
            :type="showPassword ? 'text' : 'password'" 
            placeholder="Пароль"
            class="login-input login-input--password" 
            @keydown="onKeydown" 
            required 
          />
          <button 
            type="button" 
            class="password-toggle" 
            @click="togglePasswordVisibility"
            aria-label="Показать/скрыть пароль"
          >
            <svg v-if="!showPassword" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
              <circle cx="12" cy="12" r="3" />
            </svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
              <line x1="1" y1="1" x2="23" y2="23" />
            </svg>
          </button>
        </div>

        <!-- Error message -->
        <div v-if="errorMessage" class="error-message">{{ errorMessage }}</div>

        <!-- Submit button -->
        <button type="submit" class="login-btn" :disabled="loading || isLoadingStorages">
          {{ loading ? 'Вход...' : 'Войти' }}
        </button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import HeaderBar from '@/components/HeaderBar.vue';
import { signIn } from '@/services/authService';
import { scheduleTokenRefresh } from '@/services/tokenManager';
import type { SignInRequest } from '@/types/auth.types';

const router = useRouter();

// Реактивные данные формы
// const storages = ref<StorageDefinition[]>([]);
const selectedStorage = ref<string>('');
const login = ref('');
const password = ref('');
const showPassword = ref(false);
const errorMessage = ref('');
const loading = ref(false);
const isLoadingStorages = ref(false);

// Инициализация: загружаем список хранилищ
onMounted(async () => {
  // try {
  //   storages.value = await getStorages();
  //   // Автовыбор первого хранилища для удобства (опционально)
  //   if (storages.value.length > 0) {
  //     selectedStorage.value = storages.value[0].storageId;
  //   }
  // } catch (error) {
  //   errorMessage.value = 'Не удалось загрузить список хранилищ. Проверьте подключение к серверу.';
  //   console.error('Storage load error:', error);
  // } finally {
  //   isLoadingStorages.value = false;
  // }
});

const togglePasswordVisibility = () => {
  showPassword.value = !showPassword.value;
};

const onKeydown = () => {
  if (errorMessage.value) errorMessage.value = '';
};

const handleLogin = async () => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    const req: SignInRequest = {
      login: login.value,
      password: password.value
    };
    
    await signIn(req);
    
    // Запускаем фоновое обновление токена
    scheduleTokenRefresh();
    
    router.push('/main');
    
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : 'Ошибка авторизации. Проверьте введенные данные.';
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped src="./LoginView.css"></style>