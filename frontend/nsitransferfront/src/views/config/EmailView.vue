<template>
  <div>
    <BackButton />

    <div v-if="isLoading" class="loading-text">Загрузка данных...</div>

    <div v-else class="config-card">
      <h3 class="card-title">Email-уведомления</h3>
      <div class="form-group">
        <label>Получатели уведомлений</label>
        <div class="email-list">
          <div v-for="(email, index) in emails" :key="index" class="email-input-row">
            <input 
              type="email" 
              v-model="emails[index]" 
              placeholder="email@example.com"
              :disabled="isSaving"
              @focus="handleFocus(index)"
            />
            <!-- Кнопка удаления поля, если это не последнее пустое поле -->
            <button 
              v-if="emails.length > 1 && index < emails.length - 1" 
              class="remove-email-btn" 
              @click="removeEmail(index)"
              :disabled="isSaving"
              aria-label="Удалить"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18"></line>
                <line x1="6" y1="6" x2="18" y2="18"></line>
              </svg>
            </button>
          </div>
        </div>
        <small class="hint">Нажмите на последнее поле, чтобы добавить новый адрес</small>
      </div>
      <button 
        class="save-btn" 
        :disabled="isSaving || !isEmailsValid" 
        @click="save"
      >
        {{ isSaving ? 'Сохранение...' : 'Сохранить' }}
      </button>
      <StatusMessage ref="statusMessageRef" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BackButton from '@/components/BackButton.vue';
import StatusMessage from '@/components/StatusMessage.vue';
import {
  getEmailNotifications,
  updateEmailNotifications
} from '@/services/configurationService';

const isLoading = ref(true);
const isSaving = ref(false);
const emails = ref<string[]>(['']);

const statusMessageRef = ref<InstanceType<typeof StatusMessage> | null>(null);

// Простая валидация email
const isValidEmail = (email: string): boolean => {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
};

// Валидация: хотя бы один валидный email
const isEmailsValid = computed(() => {
  const validEmails = emails.value
    .map(e => e.trim())
    .filter(e => e.length > 0);
  
  if (validEmails.length === 0) return false;
  
  return validEmails.every(email => isValidEmail(email));
});

onMounted(async () => {
  try {
    const data = await getEmailNotifications();
    // Если массив не пустой, используем его, иначе оставляем одно пустое поле
    emails.value = data.errorRecipients.length > 0 
      ? [...data.errorRecipients, ''] 
      : [''];
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Не удалось загрузить данные. Попробуйте обновить страницу.';
    console.error('Ошибка инициализации:', error);
    statusMessageRef.value?.show(message, 'error', 5000);
  } finally {
    isLoading.value = false;
  }
});

// Если фокус на последнем поле, добавляем новое пустое
const handleFocus = (index: number) => {
  if (index === emails.value.length - 1) {
    emails.value.push('');
  }
};

const removeEmail = (index: number) => {
  emails.value.splice(index, 1);
  // Гарантируем, что останется хотя бы одно поле
  if (emails.value.length === 0) {
    emails.value.push('');
  }
};

const save = async () => {
  if (!isEmailsValid.value) return;

  isSaving.value = true;
  try {
    // Фильтруем пустые строки перед отправкой
    const payload = {
      errorRecipients: emails.value.map(e => e.trim()).filter(e => e)
    };
    
    await updateEmailNotifications(payload);
    statusMessageRef.value?.show('Настройки email-уведомлений успешно сохранены', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Ошибка при сохранении настроек email-уведомлений';
    console.error('Ошибка сохранения email-уведомлений:', error);
    statusMessageRef.value?.show(message, 'error');
  } finally {
    isSaving.value = false;
  }
};
</script>

<style scoped>
.loading-text {
  color: #888;
  font-size: 14px;
  padding: 20px 0;
  text-align: center;
}
.config-card {
  background: #fff;
  padding: 24px;
  border-radius: 12px;
  box-shadow: 0 2px 10px rgba(0,0,0,0.05);
}
.card-title {
  margin-top: 0;
  margin-bottom: 20px;
  font-size: 18px;
  color: #a81b35;
  border-bottom: 2px solid #f0f0f0;
  padding-bottom: 10px;
}
.form-group {
  display: flex;
  flex-direction: column;
  margin-bottom: 16px;
}
.form-group label {
  font-size: 13px;
  color: #666;
  margin-bottom: 6px;
}
.hint {
  font-size: 12px;
  color: #999;
  margin-top: 8px;
}

.email-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.email-input-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.email-input-row input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.email-input-row input:focus {
  outline: none;
  border-color: #a81b35;
  box-shadow: 0 0 0 3px rgba(168, 27, 53, 0.1);
}
.email-input-row input:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
}

.remove-email-btn {
  background: #fff0f0;
  border: none;
  color: #e53935;
  width: 36px;
  height: 36px;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}
.remove-email-btn:hover:not(:disabled) {
  background: #ffcccc;
}
.remove-email-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.remove-email-btn svg {
  width: 18px;
  height: 18px;
}

.save-btn {
  margin-top: 10px;
  background-color: #a81b35;
  color: #fff;
  border: none;
  padding: 12px;
  border-radius: 6px;
  font-weight: 500;
  cursor: pointer;
  width: 100%;
  transition: background-color 0.2s;
}
.save-btn:hover:not(:disabled) {
  background-color: #8c1530;
}
.save-btn:disabled {
  background-color: #d1a1aa;
  cursor: not-allowed;
}
</style>