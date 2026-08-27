<template>
  <div>
    <BackButton />
    <div class="config-card">
      <h3 class="card-title">Настройка целевого справочника</h3>
      
      <div v-if="isLoading" class="loading-text">Загрузка данных...</div>
      
      <div v-else>
        <div class="form-group">
          <label>Выберите целевой справочник (он станет областью поиска объектов при синхронизации)</label>
          <div class="radio-list">
            <label 
              v-for="item in firstLayerItems" 
              :key="`${item.nodeObject.typeId}_${item.nodeObject.objectId}`" 
              class="radio-item"
            >
              <input 
                type="radio" 
                :value="`${item.nodeObject.typeId}_${item.nodeObject.objectId}`" 
                v-model="selectedNodeKey"
              />
              <span class="radio-label">{{ item.name }}</span>
            </label>
            <div v-if="firstLayerItems.length === 0" class="empty-text">
              Список доступных справочников пуст
            </div>
          </div>
        </div>

        
        <button class="save-btn" @click="save" :disabled="isSaving || !selectedNodeKey">
          {{ isSaving ? 'Сохранение...' : 'Сохранить' }}
        </button>

        <StatusMessage ref="statusMessageRef" />
        
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import BackButton from '@/components/BackButton.vue';
import StatusMessage from '@/components/StatusMessage.vue';
import { getTargetReferenceNode, updateTargetReferenceNode, type TargetReferenceNodeConfig, getFirstLayerReferences, type ClassificationTreeNode } from '@/services/configurationService';

const isLoading = ref(true);
const isSaving = ref(false);
const firstLayerItems = ref<ClassificationTreeNode[]>([]);
const selectedNodeKey = ref<string>('');
const statusMessageRef = ref<InstanceType<typeof StatusMessage> | null>(null);

onMounted(async () => {
  try {
    const [config, items] = await Promise.all([
      getTargetReferenceNode(),
      getFirstLayerReferences()
    ]);

    firstLayerItems.value = items;

    if (config.targetReferenceNodeObjectId && config.targetReferenceNodeTypeId) {
      const match = items.find(item => 
        item.nodeObject.objectId === config.targetReferenceNodeObjectId &&
        item.nodeObject.typeId === config.targetReferenceNodeTypeId
      );
      
      if (match) {
        selectedNodeKey.value = `${match.nodeObject.typeId}_${match.nodeObject.objectId}`;
      }
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Не удалось загрузить данные. Попробуйте обновить страницу.';
    console.error('Ошибка инициализации:', error);
    statusMessageRef.value?.show(message, 'error', 5000);
  } finally {
    isLoading.value = false;
  }
});

const save = async () => {
  if (!selectedNodeKey.value) return;

  isSaving.value = true;
  try {
    const [typeIdStr, objectIdStr] = selectedNodeKey.value.split('_');
    const typeId = parseInt(typeIdStr!, 10);
    const objectId = parseInt(objectIdStr!, 10);

    const selectedItem = firstLayerItems.value.find(
      item => item.nodeObject.typeId === typeId && item.nodeObject.objectId === objectId
    );

    if (!selectedItem) throw new Error('Выбранный элемент не найден в списке');

    const payload: TargetReferenceNodeConfig = {
      targetReferenceNodeObjectId: objectId,
      targetReferenceNodeTypeId: typeId,
      targetReferenceNodeName: selectedItem.name
    };

    await updateTargetReferenceNode(payload);

    statusMessageRef.value?.show('Настройки целевого справочника успешно сохранены', 'success');
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Произошла ошибка при сохранении';
    console.error('Ошибка сохранения:', error);
    statusMessageRef.value?.show(message, 'error');
  } finally {
    isSaving.value = false;
  }
};
</script>

<style scoped>
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
  margin-bottom: 12px; 
}
.loading-text, .empty-text { 
  color: #888; 
  font-size: 14px; 
  padding: 20px 0; 
  text-align: center; 
}
.radio-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 12px;
}
.radio-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.2s;
}
.radio-item:hover {
  background-color: #f9f9f9;
}
.radio-item input[type="radio"] {
  accent-color: #a81b35;
  width: 16px;
  height: 16px;
  cursor: pointer;
}
.radio-label {
  font-size: 14px;
  color: #333;
  cursor: pointer;
}
.save-btn { 
  margin-top: 20px; 
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