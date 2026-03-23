import type { NavMenu, NavMenuItems } from '~/types/nav'

export const navMenu: NavMenu[] = [
  {
    heading: 'Monitoring',
    items: [
      {
        title: 'Dashboard',
        icon: 'i-lucide-layout-dashboard',
        link: '/dashboard',
      },
      {
        title: 'Client PCs',
        icon: 'i-lucide-monitor',
        link: '/dashboard/clients',
      },
      {
        title: 'Plant Map',
        icon: 'i-lucide-map',
        link: '/dashboard/map',
      },
    ],
  },
  {
    heading: 'Management',
    items: [
      {
        title: 'Inventory',
        icon: 'i-lucide-package',
        link: '/dashboard/inventory',
      },
    ],
  },
  {
    heading: 'Administration',
    items: [
      {
        title: 'Users & Roles',
        icon: 'i-lucide-users',
        link: '/dashboard/users',
      },
      {
        title: 'Organizations',
        icon: 'i-lucide-building-2',
        link: '/dashboard/organizations',
      },
    ],
  },
]

export const navMenuBottom: NavMenuItems = [
  {
    title: 'Help & Support',
    icon: 'i-lucide-circle-help',
    link: '/dashboard/help',
  },
]
