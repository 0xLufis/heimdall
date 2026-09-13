<script setup lang="ts">
import { computed } from 'vue'
import { Button, type ButtonVariants } from '~/components/ui/button'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { Lock } from 'lucide-vue-next'
import { useRbacPermission } from '~/composables/useRbacPermission'

const props = withDefaults(defineProps<{
  hasPermission?: boolean
  capability?: string
  requiredRoles?: string[]
  tooltip?: string
  mode?: 'disable' | 'hide'
  showLockIcon?: boolean
  disabled?: boolean
  variant?: ButtonVariants['variant']
  size?: ButtonVariants['size']
  class?: string
  type?: 'button' | 'submit' | 'reset'
}>(), {
  hasPermission: undefined,
  capability: undefined,
  requiredRoles: undefined,
  tooltip: '',
  mode: 'disable',
  showLockIcon: true,
  disabled: false,
  type: 'button',
})

const emit = defineEmits<{
  (e: 'click', event: MouseEvent): void
}>()

const { userRole, checkCapability, getCapabilityTooltip } = useRbacPermission()

const isPermitted = computed(() => {
  if (props.hasPermission !== undefined) return props.hasPermission
  if (props.capability) return checkCapability(props.capability)
  if (props.requiredRoles && props.requiredRoles.length > 0) {
    return props.requiredRoles.includes(userRole.value)
  }
  return true
})

const computedTooltip = computed(() => {
  if (props.tooltip) return props.tooltip
  if (props.capability) return getCapabilityTooltip(props.capability)
  return 'You do not have required permissions for this action.'
})

function handleClick(e: MouseEvent) {
  if (!isPermitted.value || props.disabled) {
    e.preventDefault()
    e.stopPropagation()
    return
  }
  emit('click', e)
}
</script>

<template>
  <template v-if="!isPermitted && mode === 'hide'" />
  <RbacTooltip
    v-else-if="!isPermitted"
    :disabled="true"
    :tooltip="computedTooltip"
  >
    <Button
      :variant="variant"
      :size="size"
      :type="type"
      :disabled="true"
      :class="[props.class, 'opacity-60 pointer-events-none gap-1.5 select-none']"
    >
      <Lock v-if="showLockIcon" class="w-3.5 h-3.5 text-amber-400 shrink-0" />
      <slot />
    </Button>
  </RbacTooltip>
  <Button
    v-else
    :variant="variant"
    :size="size"
    :type="type"
    :disabled="disabled"
    :class="props.class"
    @click="handleClick"
  >
    <slot />
  </Button>
</template>
