<template>
  <Transition name="fade">
    <div v-if="visible" :class="['status-message', type]">
      <span class="status-icon">{{ type === 'success' ? '✓' : '✕' }}</span>
      <span class="status-text">{{ message }}</span>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref } from 'vue';

const visible = ref(false);
const message = ref('');
const type = ref<'success' | 'error'>('success');
let timer: ReturnType<typeof setTimeout> | null = null;

/**
 * Показывает плашку с сообщением.
 * @param text - текст сообщения
 * @param messageType - тип: 'success' (зелёная) или 'error' (красная)
 * @param duration - время отображения в мс (по умолчанию 3000)
 */
const show = (
  text: string,
  messageType: 'success' | 'error' = 'success',
  duration = 3000
) => {
  if (timer) {
    clearTimeout(timer);
  }
  message.value = text;
  type.value = messageType;
  visible.value = true;
  timer = setTimeout(() => {
    visible.value = false;
    timer = null;
  }, duration);
};

defineExpose({ show });
</script>

<style scoped>
.status-message {
  margin-top: 16px;
  padding: 12px;
  border-radius: 6px;
  font-weight: 500;
  font-size: 14px;
  width: 100%;
  box-sizing: border-box;
  color: #fff;
  display: flex;
  align-items: flex-start; /* Выравниваем по верху, чтобы иконка и многострочный текст выглядели аккуратно */
  gap: 8px;
  transition: background-color 0.2s;
}

.status-message.success {
  background-color: rgba(34, 197, 94, 0.7);
}

.status-message.error {
  background-color: rgba(239, 68, 68, 0.7);
}

.status-icon {
  font-weight: 700;
  font-size: 16px;
  flex-shrink: 0; /* Запрещаем иконке сжиматься */
  line-height: 1.3; /* Для лучшего вертикального выравнивания с текстом */
}

.status-text {
  line-height: 1.3;
  text-align: left; /* Для лучшей читаемости многострочного текста */
  word-break: break-word; /* Разрываем длинные слова, чтобы они не выходили за рамки */
}

/* Анимация появления/исчезновения */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.4s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>