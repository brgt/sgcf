<script setup lang="ts">
import { computed } from 'vue'
import { useAuth } from './useAuth'

const props = defineProps<{
  policy?: string
  role?: string
  /** When true, renders disabled/greyed content instead of hiding */
  passive?: boolean
}>()

const auth = useAuth()

const allowed = computed(() => {
  if (props.policy) return auth.hasPolicy(props.policy)
  if (props.role) return auth.hasRole(props.role)
  return auth.isAuthenticated
})
</script>

<template>
  <template v-if="allowed">
    <slot />
  </template>
  <template v-else-if="passive">
    <div class="role-gate-passive" style="opacity:0.4; pointer-events:none; cursor:not-allowed;">
      <slot />
    </div>
  </template>
</template>
