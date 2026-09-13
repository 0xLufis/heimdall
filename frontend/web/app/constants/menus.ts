import type { NavMenu, NavMenuItems } from '~/types/nav'
import { RBAC_TOOLTIPS } from '~/composables/useRbacPermission'

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
        title: 'Live Telemetry',
        icon: 'i-lucide-activity',
        link: '/dashboard/telemetry',
      },
      {
        title: 'Fleet Analytics',
        icon: 'i-lucide-bar-chart-3',
        link: '/dashboard/analytics',
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
        requiredCapability: 'canManageEndpoints',
        requiredTooltip: RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT,
      },
      {
        title: 'Telemetry Config',
        icon: 'i-lucide-sliders',
        link: '/dashboard/telemetry/configure',
        requiredCapability: 'canManageEndpoints',
        requiredTooltip: RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT,
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
        requiredCapability: 'canManageUsers',
        requiredTooltip: RBAC_TOOLTIPS.USER_MANAGEMENT,
      },
      {
        title: 'Organizations',
        icon: 'i-lucide-building-2',
        link: '/dashboard/organizations',
        requiredCapability: 'canAdministerSystem',
        requiredTooltip: RBAC_TOOLTIPS.ORGANIZATION_MANAGEMENT,
      },
      {
        title: 'Security Groups',
        icon: 'i-lucide-shield-check',
        link: '/dashboard/security-groups',
        requiredCapability: 'canManageActiveDirectory',
        requiredTooltip: RBAC_TOOLTIPS.IT_ADMIN,
      },
      {
        title: 'System Governance',
        icon: 'i-lucide-sliders-horizontal',
        link: '/dashboard/admin/system-settings',
        requiredCapability: 'canAdministerSystem',
        requiredTooltip: RBAC_TOOLTIPS.SYSTEM_GOVERNANCE,
      },
      {
        title: 'Identity Studio',
        icon: 'i-lucide-fingerprint',
        link: '/admin/studio',
        external: true,
        target: '_blank',
        requiredCapability: 'isSystemAdmin',
        requiredTooltip: RBAC_TOOLTIPS.SYSTEM_ADMIN,
        hideWhenUnauthorized: true,
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
