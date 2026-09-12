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
      {
        title: 'Machines',
        icon: 'i-lucide-cpu',
        link: '/dashboard/machines',
      },
      {
        title: 'Telemetry Templates',
        icon: 'i-lucide-file-code',
        link: '/dashboard/telemetry/templates',
      },
      {
        title: 'Telemetry Config',
        icon: 'i-lucide-sliders',
        link: '/dashboard/telemetry/configure',
      },
      {
        title: 'Tickets',
        icon: 'i-lucide-wrench',
        link: '/dashboard/tickets',
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
      {
        title: 'Security Groups',
        icon: 'i-lucide-shield-check',
        link: '/dashboard/security-groups',
      },
      {
        title: 'System Governance',
        icon: 'i-lucide-sliders-horizontal',
        link: '/dashboard/admin/system-settings',
      },
      {
        title: 'Identity Studio',
        icon: 'i-lucide-fingerprint',
        link: '/admin/studio',
        external: true,
        target: '_blank',
      },
    ],
  },
]

export const navMenuBottom: NavMenuItems = [
  {
    title: 'User Settings',
    icon: 'i-lucide-settings',
    link: '/dashboard/settings',
  },
  {
    title: 'Help & Support',
    icon: 'i-lucide-circle-help',
    link: '/dashboard/help',
  },
]
