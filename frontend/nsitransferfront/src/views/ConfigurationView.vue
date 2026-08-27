<template>
  <div class="config-container">
    <!-- Меню секций -->
    <div v-if="isMenuVisible" class="config-menu">
      <button class="menu-item" @click="goTo('target-node')">
        <div class="menu-text">
          <h3>Настройка целевого справочника</h3>
          <p>Установка области поиска объектов</p>
        </div>
        <svg class="menu-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
      </button>

      <button class="menu-item" @click="goTo('rabbitmq')">
        <div class="menu-text">
          <h3>Очереди и параметры повторов отправок в RabbitMQ</h3>
          <p>Имена очередей, количество попыток, интервалы и таймауты</p>
        </div>
        <svg class="menu-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
      </button>

      <button class="menu-item" @click="goTo('polynom')">
        <div class="menu-text">
          <h3>Конфигурация параметров работы с ПОЛИНОМ:MDM</h3>
          <p>Интервал синхронизации</p>
        </div>
        <svg class="menu-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
      </button>

      <button class="menu-item" @click="goTo('email')">
        <div class="menu-text">
          <h3>Email-уведомления</h3>
          <p>Список получателей уведомлений об ошибках</p>
        </div>
        <svg class="menu-chevron" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
      </button>
    </div>

    <!-- Вложенные маршруты (карточки) -->
    <div v-else class="config-details">
      <router-view />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const route = useRoute();
const router = useRouter();

// Меню видно только если мы строго на /main/config
const isMenuVisible = computed(() => route.path === '/main/config');

const goTo = (section: string): void => {
  router.push(`/main/config/${section}`);
};
</script>

<style scoped src="./ConfigurationView.css"></style>