<script setup lang="ts">
import { GlobeIcon, CheckIcon } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAppI18n, type SupportedLocale } from '~/composables/useAppI18n'

const props = withDefaults(defineProps<{
  variant?: 'dropdown' | 'inline'
  size?: 'sm' | 'default'
}>(), {
  variant: 'dropdown',
  size: 'sm'
})

const { locale, setLocale, supportedLocales, t } = useAppI18n()

function handleSelectLocale(code: SupportedLocale) {
  setLocale(code)
}
</script>

<template>
  <div v-if="variant === 'inline'" class="flex items-center gap-2">
    <Button
      v-for="loc in supportedLocales"
      :key="loc.code"
      type="button"
      :variant="locale === loc.code ? 'default' : 'outline'"
      size="sm"
      class="text-xs flex items-center gap-1.5"
      :data-testid="`locale-btn-${loc.code}`"
      @click="handleSelectLocale(loc.code)"
    >
      <span>{{ loc.flag }}</span>
      <span class="font-medium">{{ loc.nativeName }}</span>
      <CheckIcon v-if="locale === loc.code" class="w-3.5 h-3.5 ml-1" />
    </Button>
  </div>

  <DropdownMenu v-else>
    <DropdownMenuTrigger as-child>
      <Button
        variant="ghost"
        :size="size"
        class="h-8 px-2 flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground"
        data-testid="language-switcher-trigger"
        :aria-label="t('language.select', 'Select Language')"
      >
        <GlobeIcon class="w-3.5 h-3.5" />
        <span class="font-semibold uppercase tracking-wider">{{ locale }}</span>
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="end" class="w-40">
      <DropdownMenuLabel class="text-xs font-semibold text-muted-foreground">
        {{ t('language.select', 'Language') }}
      </DropdownMenuLabel>
      <DropdownMenuSeparator />
      <DropdownMenuItem
        v-for="loc in supportedLocales"
        :key="loc.code"
        class="flex items-center justify-between text-xs cursor-pointer"
        :data-testid="`locale-option-${loc.code}`"
        @click="handleSelectLocale(loc.code)"
      >
        <div class="flex items-center gap-2">
          <span>{{ loc.flag }}</span>
          <span>{{ loc.nativeName }}</span>
        </div>
        <CheckIcon v-if="locale === loc.code" class="w-3.5 h-3.5 text-primary" />
      </DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
