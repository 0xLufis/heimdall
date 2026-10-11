<script setup lang="ts">
import Search from '~/components/Search.vue'
import LanguageSwitcher from '~/components/common/LanguageSwitcher.vue'
import NotificationCenterPopover from '~/components/notifications/NotificationCenterPopover.vue'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import { useAppI18n } from '~/composables/useAppI18n'

const route = useRoute()
const { t } = useAppI18n()

/**
 * Dynamically generates breadcrumb links based on the current route.
 * Converts URL segments into human-readable titles and constructs their corresponding HREFs.
 * @returns {Array<object>} An array of link objects, each with a `title` and `href`.
 */
function setLinks() {
  if (route.fullPath === '/') {
    return [{ title: t('nav.home', 'Home'), href: '/' }]
  }

  const segments = route.fullPath.split('/').filter(item => item !== '')

  const breadcrumbs = segments.map((item, index) => {
    const str = item.replace(/-/g, ' ')
    const title = str
      .split(' ')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(' ')

    return {
      title,
      href: `/${segments.slice(0, index + 1).join('/')}`,
    }
  })

  return [{ title: t('nav.home', 'Home'), href: '/' }, ...breadcrumbs]
}

/**
 * Reactive reference to the breadcrumb links, updated whenever the route changes.
 * @type {Ref<Array<{ title: string; href: string }>>}
 */
const links = ref<{
  title: string
  href: string
}[]>(setLinks())

watch(() => route.fullPath, (val) => {
  if (val) {
    links.value = setLinks()
  }
})

const { triggerSearch } = useGlobalSearchModal()
</script>

<template>
  <header class="sticky top-0 md:peer-data-[variant=inset]:top-2 z-10 h-(--header-height) flex items-center gap-4 border-b bg-background px-4 md:px-6 md:rounded-tl-xl md:rounded-tr-xl">
    <div class="w-full flex items-center gap-4 h-4">
      <SidebarTrigger />
      <Separator orientation="vertical" />
      <BaseBreadcrumbCustom :links="links" />
    </div>
    <div class="ml-auto flex items-center gap-2">
      <slot />
      <!-- Global Search Floating at the Page Top Header -->
      <Search class="w-44 sm:w-60 lg:w-72 px-0 py-0 hidden sm:block" />
      <button
        type="button"
        class="sm:hidden p-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/80 transition-colors cursor-pointer"
        :title="t('common.openGlobalSearch', 'Search') + ' (⌘K)'"
        :aria-label="t('common.openGlobalSearch', 'Open Search')"
        @click="triggerSearch"
      >
        <Icon name="i-lucide-search" class="size-4" />
      </button>
      <NotificationCenterPopover />
      <LanguageSwitcher />
    </div>
  </header>
</template>

<style scoped>

</style>
