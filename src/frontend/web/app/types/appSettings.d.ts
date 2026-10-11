export interface AppSettings {
  sidebar?: {
    collapsible?: 'offcanvas' | 'icon' | 'none'
    side?: 'left' | 'right'
    variant?: 'sidebar' | 'floating' | 'inset'
  }
  theme?: {
    color?: ThemeColor
    type?: ThemeType
  }
  ai?: {
    enableAiImport?: boolean
    aiApiToken?: string
    aiProvider?: 'cloud' | 'local'
    endpoint?: string
  }
}
