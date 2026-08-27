<template>
  <div>
    <BackButton />

    <div v-if="isLoading" class="loading-text">Загрузка данных...</div>

    <template v-else>
      <div class="config-card">
        <h3 class="card-title">Очереди и обменники RabbitMQ</h3>
        
        <div class="form-group">
          <label>Имя обменника NsiTransfer</label>
          <input
            type="text"
            v-model="queues.nsiTransferExchangeName"
            :disabled="isSavingQueues"
            placeholder="Например: nsi_transfer_exchange"
          />
        </div>

        <div class="form-group">
          <label>Имя очереди результатов поиска</label>
          <input
            type="text"
            v-model="queues.polynomSearchResultsQueueName"
            :disabled="isSavingQueues"
            placeholder="Например: polynom_search_results"
          />
        </div>

        <button
          class="save-btn"
          :disabled="isSavingQueues || !isQueuesValid"
          @click="saveQueues"
        >
          {{ isSavingQueues ? 'Сохранение...' : 'Сохранить' }}
        </button>
        <StatusMessage ref="queuesStatusRef" />
      </div>

      <div class="config-card" style="margin-top: 20px;">
        <h3 class="card-title">Параметры повторов отправок в RabbitMQ</h3>
        <div class="form-group">
          <label>Макс. количество попыток</label>
          <input
            type="number"
            min="1"
            v-model.number="retry.maxRetryAttempts"
            :disabled="isSavingRetry"
          />
        </div>
        <div class="form-group">
          <label>Интервал между попытками (сек)</label>
          <input
            type="number"
            min="1"
            v-model.number="retry.retryIntervalInSeconds"
            :disabled="isSavingRetry"
          />
        </div>
        <div class="form-group">
          <label>Таймаут подтверждения (сек)</label>
          <input
            type="number"
            min="1"
            v-model.number="retry.confirmationTimeOutInSeconds"
            :disabled="isSavingRetry"
          />
        </div>
        <button
          class="save-btn"
          :disabled="isSavingRetry || !isRetryValid"
          @click="saveRetry"
        >
          {{ isSavingRetry ? 'Сохранение...' : 'Сохранить' }}
        </button>
        <StatusMessage ref="retryStatusRef" />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BackButton from '@/components/BackButton.vue';
import StatusMessage from '@/components/StatusMessage.vue';
import {
  getRabbitMqQueues,
  updateRabbitMqQueues,
  getRabbitMqRetryParams,
  updateRabbitMqRetryParams,
  type RabbitMqQueues,
  type RabbitMqRetryParams
} from '@/services/configurationService';

const isLoading = ref(true);
const isSavingQueues = ref(false);
const isSavingRetry = ref(false);

// Обновленная структура в соответствии с новой моделью на сервере
const queues = ref<RabbitMqQueues>({
  nsiTransferExchangeName: '',
  polynomSearchResultsQueueName: ''
});

const retry = ref<RabbitMqRetryParams>({
  maxRetryAttempts: 1,
  retryIntervalInSeconds: 1,
  confirmationTimeOutInSeconds: 1
});

const queuesStatusRef = ref<InstanceType<typeof StatusMessage> | null>(null);
const retryStatusRef = ref<InstanceType<typeof StatusMessage> | null>(null);

// Валидация: оба поля должны быть заполнены
const isQueuesValid = computed(() => {
  return (
    queues.value.nsiTransferExchangeName.trim().length > 0 &&
    queues.value.polynomSearchResultsQueueName.trim().length > 0
  );
});

// Валидация: все числа > 0
const isRetryValid = computed(() => {
  const r = retry.value;
  return (
    Number.isInteger(r.maxRetryAttempts) && r.maxRetryAttempts > 0 &&
    Number.isInteger(r.retryIntervalInSeconds) && r.retryIntervalInSeconds > 0 &&
    Number.isInteger(r.confirmationTimeOutInSeconds) && r.confirmationTimeOutInSeconds > 0
  );
});

onMounted(async () => {
  try {
    const [queuesData, retryData] = await Promise.all([
      getRabbitMqQueues(),
      getRabbitMqRetryParams()
    ]);
    queues.value = queuesData;
    retry.value = retryData;
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Не удалось загрузить данные. Попробуйте обновить страницу.';
    console.error('Ошибка инициализации:', error);
    queuesStatusRef.value?.show(message, 'error', 5000);
  } finally {
    isLoading.value = false;
  }
});

const saveQueues = async () => {
  if (!isQueuesValid.value) return;

  isSavingQueues.value = true;
  try {
    await updateRabbitMqQueues({
      nsiTransferExchangeName: queues.value.nsiTransferExchangeName.trim(),
      polynomSearchResultsQueueName: queues.value.polynomSearchResultsQueueName.trim()
    });
    queuesStatusRef.value?.show('Настройки очередей и обменников успешно сохранены', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Ошибка при сохранении настроек очередей';
    console.error('Ошибка сохранения очередей:', error);
    queuesStatusRef.value?.show(message, 'error');
  } finally {
    isSavingQueues.value = false;
  }
};

const saveRetry = async () => {
  if (!isRetryValid.value) return;

  isSavingRetry.value = true;
  try {
    await updateRabbitMqRetryParams({
      maxRetryAttempts: retry.value.maxRetryAttempts,
      retryIntervalInSeconds: retry.value.retryIntervalInSeconds,
      confirmationTimeOutInSeconds: retry.value.confirmationTimeOutInSeconds
    });
    retryStatusRef.value?.show('Параметры повторов успешно сохранены', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Ошибка при сохранении параметров повторов';
    console.error('Ошибка сохранения параметров повторов:', error);
    retryStatusRef.value?.show(message, 'error');
  } finally {
    isSavingRetry.value = false;
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
.form-group input {
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.form-group input:focus {
  outline: none;
  border-color: #a81b35;
  box-shadow: 0 0 0 3px rgba(168, 27, 53, 0.1);
}
.form-group input:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
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