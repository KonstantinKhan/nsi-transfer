<script setup lang="ts">
import { RouterView } from 'vue-router'
import { onMounted } from 'vue';
import { initTokenManager } from '@/services/tokenManager';

onMounted(() => {
  initTokenManager();
});
</script>

<template>
  <div class="app-shell">
    <div class="app-content">
      <RouterView />
    </div>
  </div>

</template>
<style src="./styles/reset-styles.css"></style>
<style>
html,
body,
#app {
  height: 100%;
  margin: 0;
  overflow: hidden;
}

body {
  font-family: Inter, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
}
</style>

<style scoped>
.app-shell {
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
}

.app-shell {
  flex: 1 1 0;
  min-height: 0;
  overflow: auto;
}
</style>
