export interface NavLink {
  title: string
  link: string
  icon?: string
  new?: boolean
  external?: boolean
  target?: string
  requiredCapability?: string
  requiredTooltip?: string
  hideWhenUnauthorized?: boolean
  requiredModule?: 'core' | 'ticketing' | 'inventory' | 'telemetry'
}

export interface NavSectionTitle {
  heading: string
  requiredModule?: 'core' | 'ticketing' | 'inventory' | 'telemetry'
}

export interface NavGroup {
  title: string
  icon?: string
  new?: boolean
  children: NavLink[]
  requiredModule?: 'core' | 'ticketing' | 'inventory' | 'telemetry'
}

export interface NavMenu {
  heading: string
  items: NavMenuItems
  requiredModule?: 'core' | 'ticketing' | 'inventory' | 'telemetry'
}

export declare type NavMenuItems = (NavLink | NavGroup | NavSectionTitle)[]
