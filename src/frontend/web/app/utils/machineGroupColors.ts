export interface MachineGroupColorStyle {
  name: string
  border: string
  bg: string
  text: string
  badge: string
  hex: string
}

const PALETTE: MachineGroupColorStyle[] = [
  {
    name: 'blue',
    border: 'border-blue-500/40 dark:border-blue-500/50',
    bg: 'bg-blue-500/10 dark:bg-blue-500/15',
    text: 'text-blue-600 dark:text-blue-400',
    badge: 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border-blue-500/30',
    hex: '#3b82f6'
  },
  {
    name: 'violet',
    border: 'border-violet-500/40 dark:border-violet-500/50',
    bg: 'bg-violet-500/10 dark:bg-violet-500/15',
    text: 'text-violet-600 dark:text-violet-400',
    badge: 'bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30',
    hex: '#8b5cf6'
  },
  {
    name: 'emerald',
    border: 'border-emerald-500/40 dark:border-emerald-500/50',
    bg: 'bg-emerald-500/10 dark:bg-emerald-500/15',
    text: 'text-emerald-600 dark:text-emerald-400',
    badge: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30',
    hex: '#10b981'
  },
  {
    name: 'amber',
    border: 'border-amber-500/40 dark:border-amber-500/50',
    bg: 'bg-amber-500/10 dark:bg-amber-500/15',
    text: 'text-amber-600 dark:text-amber-400',
    badge: 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30',
    hex: '#f59e0b'
  },
  {
    name: 'rose',
    border: 'border-rose-500/40 dark:border-rose-500/50',
    bg: 'bg-rose-500/10 dark:bg-rose-500/15',
    text: 'text-rose-600 dark:text-rose-400',
    badge: 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30',
    hex: '#f43f5e'
  },
  {
    name: 'cyan',
    border: 'border-cyan-500/40 dark:border-cyan-500/50',
    bg: 'bg-cyan-500/10 dark:bg-cyan-500/15',
    text: 'text-cyan-600 dark:text-cyan-400',
    badge: 'bg-cyan-500/15 text-cyan-700 dark:text-cyan-300 border-cyan-500/30',
    hex: '#06b6d4'
  },
  {
    name: 'indigo',
    border: 'border-indigo-500/40 dark:border-indigo-500/50',
    bg: 'bg-indigo-500/10 dark:bg-indigo-500/15',
    text: 'text-indigo-600 dark:text-indigo-400',
    badge: 'bg-indigo-500/15 text-indigo-700 dark:text-indigo-300 border-indigo-500/30',
    hex: '#6366f1'
  },
  {
    name: 'teal',
    border: 'border-teal-500/40 dark:border-teal-500/50',
    bg: 'bg-teal-500/10 dark:bg-teal-500/15',
    text: 'text-teal-600 dark:text-teal-400',
    badge: 'bg-teal-500/15 text-teal-700 dark:text-teal-300 border-teal-500/30',
    hex: '#14b8a6'
  }
]

export function getGroupColor(groupNameOrId?: string): MachineGroupColorStyle {
  if (!groupNameOrId) {
    return {
      name: 'neutral',
      border: 'border-border',
      bg: 'bg-muted/40',
      text: 'text-muted-foreground',
      badge: 'bg-muted text-muted-foreground border-border',
      hex: '#6b7280'
    }
  }

  // Hash the group name deterministically
  let hash = 0
  for (let i = 0; i < groupNameOrId.length; i++) {
    hash = (hash << 5) - hash + groupNameOrId.charCodeAt(i)
    hash |= 0
  }
  const index = Math.abs(hash) % PALETTE.length
  return PALETTE[index]
}
