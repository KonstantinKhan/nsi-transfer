<template>
  <nav class="footer-container">
    <div class="footer-bar">
      <!-- Анимированный индикатор -->
      <div class="indicator" :class="{ 'is-ready': isReady }" :style="indicatorStyle"></div>
      
      <button 
        v-for="(btn, index) in buttons" 
        :key="btn.id"
        class="footer-btn"
        :class="{ active: activeIndex === index }"
        ref="btnRefs"
        @click="selectButton(index, btn)"
      >
        <span class="btn-icon" v-html="btn.icon"></span>
        <span class="btn-title">{{ btn.title }}</span>
      </button>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, nextTick, watch } from 'vue';

interface FooterButton {
  id: string;
  title: string;
  icon: string;
  route: string;
}

const props = withDefaults(defineProps<{
  buttons: FooterButton[];
  currentRoute?: string;
}>(), {
  currentRoute: ''
});

const emit = defineEmits(['navigate']);

const btnRefs = ref<HTMLButtonElement[]>([]);

const getActiveIndex = (route: string): number => {
  let bestIndex = -1;
  let bestLength = 0;

  props.buttons.forEach((b, index) => {
    if (route === b.route || route.startsWith(b.route + '/')) {
      if (b.route.length > bestLength) {
        bestLength = b.route.length;
        bestIndex = index;
      }
    }
  });

  return bestIndex;
};

/*
 * Важно: индекс считаем СРАЗУ, синхронно, на основе currentRoute,
 * а не дефолтным нулём с последующей корректировкой в onMounted.
 * Раньше кнопка "Синхронизация" (index 0) на первом рендере всегда
 * становилась активной по умолчанию, и только потом, уже в onMounted,
 * индекс поправлялся на реальный (например "Настройки"). Из-за этого
 * text-color transition на кнопке успевал сыграть переключение
 * "Синхронизация -> Настройки" — это и был один из источников
 * "анимации из ниоткуда".
 */
const initialIndex = getActiveIndex(props.currentRoute);
const activeIndex = ref(initialIndex !== -1 ? initialIndex : 0);

// Индикатор изначально невидим (opacity: 0) и без transition.
// Он становится видимым ТОЛЬКО после того, как мы посчитали его
// реальные width/transform по активной кнопке. Так как реальная
// позиция выставляется до появления индикатора, пользователь
// физически не может увидеть "наезд"/скольжение из нулевой позиции —
// это не зависит от тонкостей таймингов рендера в разных браузерах.
const isReady = ref(false);
const indicatorStyle = ref({
  width: '0px',
  transform: 'translateX(0px)'
});

const updateIndicator = () => {
  nextTick(() => {
    const activeBtn = btnRefs.value[activeIndex.value];
    if (activeBtn) {
      indicatorStyle.value = {
        width: `${activeBtn.offsetWidth}px`,
        transform: `translateX(${activeBtn.offsetLeft}px)`
      };
    }
  });
};

const selectButton = (index: number, btn: FooterButton): void => {
  activeIndex.value = index;
  updateIndicator();
  emit('navigate', btn.route);
};

onMounted(() => {
  nextTick(() => {
    const activeBtn = btnRefs.value[activeIndex.value];
    if (activeBtn) {
      indicatorStyle.value = {
        width: `${activeBtn.offsetWidth}px`,
        transform: `translateX(${activeBtn.offsetLeft}px)`
      };
    }

    // Двойной requestAnimationFrame — доп. подстраховка, чтобы браузер
    // гарантированно закоммитил состояние "без transition" отдельным
    // кадром, прежде чем мы включим анимацию и покажем индикатор.
    // Одного rAF в части браузерных движков может быть недостаточно.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        isReady.value = true;
      });
    });
  });
  
  window.addEventListener('resize', updateIndicator);
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateIndicator);
});

watch(() => props.currentRoute, (newRoute) => {
  const index = getActiveIndex(newRoute);
  if (index !== -1) {
    activeIndex.value = index;
    updateIndicator();
  }
});
</script>

<style scoped>
.footer-container {
  position: fixed;
  bottom: 24px;
  left: 0;
  right: 0;
  display: flex;
  justify-content: center;
  z-index: 1000;
}

.footer-bar {
  position: relative;
  display: flex;
  background-color: #ffffff;
  padding: 8px;
  border-radius: 9999px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
  gap: 4px;
}

.indicator {
  position: absolute;
  top: 8px;
  left: 0;
  height: calc(100% - 16px);
  background-color: #a81b35;
  border-radius: 9999px;
  z-index: 1;
  opacity: 0; /* скрыт, пока не посчитана реальная позиция */
  transition: none; /* по умолчанию анимация отключена */
}

/* Анимация включается только когда компонент готов.
   Обратите внимание: opacity сюда намеренно не входит — переход
   invisible -> visible происходит мгновенно, а не плавно, чтобы
   при появлении не было эффекта "въезда" индикатора. */
.indicator.is-ready {
  opacity: 1;
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), width 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.footer-btn {
  position: relative;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: none;
  padding: 8px 20px;
  cursor: pointer;
  color: #666666;
  transition: color 0.3s ease;
  min-width: 80px;
}

.footer-btn.active {
  color: #ffffff;
}

.btn-icon {
  display: flex;
  width: 24px;
  height: 24px;
  margin-bottom: 4px;
}

.btn-icon :deep(svg) {
  width: 100%;
  height: 100%;
}

.btn-title {
  font-size: 12px;
  font-weight: 500;
}
</style>