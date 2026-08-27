<template>
  <header class="header-bar">
    <div class="header-content">
      <h1 class="app-title">НСИ Трансфер</h1>
      
      <button
        v-if="showLogoutButton"
        @click="handleLogout"
        class="logout-btn"
        title="Выйти из системы"
        aria-label="Выйти из системы"
      >
        <svg 
          xmlns="http://www.w3.org/2000/svg" 
          width="24" 
          height="24" 
          viewBox="0 0 24 24" 
          fill="none" 
          stroke="currentColor" 
          stroke-width="2" 
          stroke-linecap="round" 
          stroke-linejoin="round"
        >
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
          <polyline points="16 17 21 12 16 7"></polyline>
          <line x1="21" y1="12" x2="9" y2="12"></line>
        </svg>
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { signOut } from '@/services/authService'; 
import { useRouter } from 'vue-router';

const router = useRouter();

defineProps<{
  showLogoutButton?: boolean;
}>();

const handleLogout = async () => {
  try {
    await signOut();
    router.push('/login');
  } catch (error) {
    console.error('Ошибка при выходе из системы:', error);
  }
};
</script>

<style scoped>
.header-bar {
  background-color: #a81b35;
  color: #ffffff;
  width: 100%;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.header-content {
  padding: 16px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between; /* Разносит заголовок и кнопку по разным краям */
}

.app-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
}

/* Стили для кнопки выхода */
.logout-btn {
  background: transparent;
  border: none;
  color: #ffffff;
  cursor: pointer;
  padding: 8px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s ease, transform 0.1s ease;
}

.logout-btn:hover {
  background-color: rgba(255, 255, 255, 0.15);
}

.logout-btn:active {
  transform: scale(0.95);
}

.logout-btn svg {
  width: 24px;
  height: 24px;
}
</style>