<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<template>
  <div style="display:flex; flex-direction:column; align-items:center; justify-content:center; min-height:100vh; gap:16px;">
    <h1 style="font-size:48px; color:#e74c3c;">403</h1>
    <p>Você não tem permissão para acessar este recurso.</p>
    <button @click="router.back()" style="padding:8px 16px; cursor:pointer;">Voltar</button>
  </div>
</template>
