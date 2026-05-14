<script setup lang="ts">
import { RouterView } from 'vue-router'
import SidebarNav from './components/SidebarNav.vue'
import TopBar from './components/TopBar.vue'
</script>

<template>
  <div class="app-shell">
    <!-- Fixed sidebar -->
    <aside class="app-shell__sidebar" aria-label="Barra lateral">
      <SidebarNav />
    </aside>

    <!-- Main content area -->
    <div class="app-shell__main">
      <TopBar />

      <main class="app-shell__content" id="main-content" tabindex="-1">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-shell {
  display: flex;
  min-height: 100vh;
  background: var(--color-background);
}

/* Sidebar */
.app-shell__sidebar {
  width: 240px;
  flex-shrink: 0;
  background: #0c141a;
  display: flex;
  flex-direction: column;
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  overflow: hidden;
  z-index: 100;
}

/* Main area offset to account for fixed sidebar */
.app-shell__main {
  flex: 1;
  margin-left: 240px;
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  min-width: 0;
}

/* Page content */
.app-shell__content {
  flex: 1;
  padding: 1.5rem;
  overflow: auto;
}

/* Focus outline for skip-to-content accessibility */
.app-shell__content:focus {
  outline: 2px solid var(--color-primary, #00f9b8);
  outline-offset: -2px;
}
</style>
