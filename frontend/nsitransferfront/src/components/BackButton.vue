<template>
  <button class="back-btn" @click="$router.push('/main/config')" aria-label="Назад">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
      <line x1="19" y1="12" x2="5" y2="12"></line>
      <polyline points="12 19 5 12 12 5"></polyline>
    </svg>
  </button>
</template>

<script setup lang="ts">
// Использует router-link или $router.push неявно через $router
</script>

<style scoped>
.back-btn {
  top: 20px;
  left: 20px;
  background: #ffffff;
  border: 1px solid #e0e0e0;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #a81b35;
  box-shadow: 0 2px 8px rgba(0,0,0,0.05);
  transition: all 0.2s ease;
  z-index: 10;

  margin-bottom: 15px;
}

.back-btn:hover {
  background-color: #a81b35;
  color: #ffffff;
  border-color: #a81b35;
  transform: translateX(-3px);
}

.back-btn svg {
  width: 20px;
  height: 20px;
}
</style>