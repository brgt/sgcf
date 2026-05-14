<script setup lang="ts">
import { Skeleton } from '@nordware/design-system'

defineProps<{
  loading?: boolean
  /** Number of skeleton rows to show */
  rows?: number
}>()
</script>

<template>
  <template v-if="loading">
    <div v-for="i in (rows ?? 5)" :key="i" style="margin-bottom:8px;">
      <Skeleton height="20px" />
    </div>
  </template>
  <slot v-else />
</template>
