export default defineAppConfig({
  icon: {
    size: '20px', // default <Icon> size applied
    class: '', // default <Icon> class applied
  },
  appSettings: {
    sidebar: {
      collapsible: 'icon', // 'offcanvas' | 'icon' | 'none'
      side: 'left', // 'left' | 'right'
      variant: 'inset', // 'sidebar' | 'floating' | 'inset'
    },
  },
})
