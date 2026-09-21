<template>
  <div>
    <BackButton />

    <div v-if="isLoading" class="loading-text">Загрузка данных...</div>

    <template v-else>
      <!-- <div class="config-card">
        <h3 class="card-title">Конфигурация Polynom</h3>
        <div class="form-group">
          <label>Адрес сервера Polynom</label>
          <input
            type="text"
            v-model="config.address"
            :disabled="isSavingConfig"
            placeholder="http://polynom.server.local:8080"
          />
        </div>
        <div class="form-group">
          <label>Имя базы данных</label>
          <input
            type="text"
            v-model="config.dbName"
            :disabled="isSavingConfig"
            placeholder="PolynomMainDB"
          />
        </div>
        <div class="form-group">
          <label>Идентификатор часового пояса</label>
          <input
            type="text"
            v-model="config.timeZoneId"
            :disabled="isSavingConfig"
            placeholder="Russian Standard Time"
          />
        </div>
        <button
          class="save-btn"
          :disabled="isSavingConfig || !isConfigValid"
          @click="saveConfig"
        >
          {{ isSavingConfig ? 'Сохранение...' : 'Сохранить' }}
        </button>
        <StatusMessage ref="configStatusRef" />
      </div> -->

      <div class="config-card" style="margin-top: 20px;">
        <h3 class="card-title">Опции синхронизации API Полином</h3>
        <div class="form-group">
          <label>Интервал синхронизации (мин)</label>
          <input
            type="number"
            min="1"
            v-model.number="syncOptions.startSyncWithIntervalMinutes"
            :disabled="isSavingSync"
          />
        </div>
        <button
          class="save-btn"
          :disabled="isSavingSync || !isSyncValid"
          @click="saveSyncOptions"
        >
          {{ isSavingSync ? 'Сохранение...' : 'Сохранить' }}
        </button>
        <StatusMessage ref="syncStatusRef" />
      </div>

      <div class="config-card" style="margin-top: 20px;">
        <h3 class="card-title">Переиндексация кодов классификатора</h3>
        <p class="card-description">Переиндексирует кеш последних кодов классификатора, обойдя все группы справочника через Polynom API</p>
        <button
          class="rebuild-cache-btn"
          :disabled="isRebuildingCache"
          @click="handleRebuildGroupCodeCache"
        >
          <svg v-if="isRebuildingCache" class="spinner" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
          </svg>
          {{ isRebuildingCache ? 'Запуск...' : 'Переиндексировать коды классификатора' }}
        </button>
        <StatusMessage ref="rebuildStatusRef" />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BackButton from '@/components/BackButton.vue';
import StatusMessage from '@/components/StatusMessage.vue';
import {
  getPolynomConfig,
  updatePolynomConfig,
  getPolynomApiSyncOptions,
  updatePolynomApiSyncOptions,
  type PolynomConfig,
  type PolynomApiSyncOptions
} from '@/services/configurationService';
import { rebuildGroupCodeCache } from '@/services/polynomSyncService';

const isLoading = ref(true);
const isSavingConfig = ref(false);
const isSavingSync = ref(false);
const isRebuildingCache = ref(false);

const config = ref<PolynomConfig>({
  address: '',
  dbName: '',
  timeZoneId: ''
});

const syncOptions = ref<PolynomApiSyncOptions>({
  startSyncWithIntervalMinutes: 1,
  conceptNameForClassificationData: '',
  classificationCodePropertyName: '',
  ownContractName: '',
  minCodePropertyName: '',
  maxCodePropertyName: ''
});

const configStatusRef = ref<InstanceType<typeof StatusMessage> | null>(null);
const syncStatusRef = ref<InstanceType<typeof StatusMessage> | null>(null);
const rebuildStatusRef = ref<InstanceType<typeof StatusMessage> | null>(null);

// Валидация: все строки не пустые
const isConfigValid = computed(() => {
  const c = config.value;
  return (
    c.address.trim().length > 0 &&
    c.dbName.trim().length > 0 &&
    c.timeZoneId.trim().length > 0
  );
});

// Валидация: число > 0 и целое
const isSyncValid = computed(() => {
  const v = syncOptions.value.startSyncWithIntervalMinutes;
  return Number.isInteger(v) && v > 0;
});

onMounted(async () => {
  try {
    const [syncData] = await Promise.all([
      // getPolynomConfig(),
      getPolynomApiSyncOptions()
    ]);
    // config.value = configData;
    syncOptions.value = syncData;
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Не удалось загрузить данные. Попробуйте обновить страницу.';
    console.error('Ошибка инициализации:', error);
    syncStatusRef.value?.show(message, 'error', 5000);
  } finally {
    isLoading.value = false;
  }
});

const saveConfig = async () => {
  if (!isConfigValid.value) return;

  isSavingConfig.value = true;
  try {
    await updatePolynomConfig({
      address: config.value.address.trim(),
      dbName: config.value.dbName.trim(),
      timeZoneId: config.value.timeZoneId.trim()
    });
    configStatusRef.value?.show('Конфигурация Polynom успешно сохранена', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Ошибка при сохранении конфигурации Polynom';
    console.error('Ошибка сохранения конфигурации Polynom:', error);
    configStatusRef.value?.show(message, 'error');
  } finally {
    isSavingConfig.value = false;
  }
};

const saveSyncOptions = async () => {
  if (!isSyncValid.value) return;

  isSavingSync.value = true;
  try {
    await updatePolynomApiSyncOptions({
      ...syncOptions.value,
      startSyncWithIntervalMinutes: syncOptions.value.startSyncWithIntervalMinutes
    });
    syncStatusRef.value?.show('Опции синхронизации успешно сохранены', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Ошибка при сохранении опций синхронизации';
    console.error('Ошибка сохранения опций синхронизации:', error);
    syncStatusRef.value?.show(message, 'error');
  } finally {
    isSavingSync.value = false;
  }
};

const handleRebuildGroupCodeCache = async () => {
  if (!confirm('Переиндексация обойдёт все группы справочника через Polynom API и может занимать продолжительное время. Запустить?')) {
    return;
  }

  isRebuildingCache.value = true;
  try {
    const result = await rebuildGroupCodeCache();
    rebuildStatusRef.value?.show(result.message, 'success');
  } catch (error) {
    console.error('Ошибка запуска переиндексации:', error);
    rebuildStatusRef.value?.show(
      error instanceof Error ? error.message : 'Не удалось запустить переиндексацию кеша кодов классификатора',
      'error'
    );
  } finally {
    isRebuildingCache.value = false;
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
.card-description {
  margin: 0 0 16px 0;
  font-size: 14px;
  color: #666;
}
.rebuild-cache-btn {
  background-color: #3498db;
  color: #fff;
  border: none;
  padding: 12px 28px;
  font-size: 15px;
  font-weight: 600;
  border-radius: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 10px;
  box-shadow: 0 4px 15px rgba(52, 152, 219, 0.3);
  transition: transform 0.1s, box-shadow 0.2s, background-color 0.2s;
  width: 100%;
  justify-content: center;
}
.rebuild-cache-btn:hover:not(:disabled) {
  background-color: #2980b9;
  box-shadow: 0 6px 20px rgba(52, 152, 219, 0.4);
}
.rebuild-cache-btn:active:not(:disabled) {
  transform: scale(0.98);
}
.rebuild-cache-btn:disabled {
  background-color: #a9cce3;
  cursor: not-allowed;
}
.spinner {
  width: 18px;
  height: 18px;
  animation: spin 1s linear infinite;
}
@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>