export type MfaThreshold = 'always' | '12h' | '24h' | '7d' | '14d' | '30d' | '90d' | 'custom' | 'never'

export interface MfaGroupRule {
  id: string
  targetType: 'role' | 'group'
  targetName: string
  forceMfa: boolean
  timeoutThreshold: MfaThreshold
  customDays?: number
  description?: string
}

export interface MfaPolicy {
  enabled: boolean
  defaultThreshold: MfaThreshold
  rules: MfaGroupRule[]
}

export interface MfaEvaluationRequest {
  role?: string
  groups?: string[]
  lastMfaAt?: string
}

export interface MfaEvaluationResult {
  mfaRequired: boolean
  reason: string
  matchedRuleTarget?: string
  appliedThreshold: string
  expiresAt?: string
  isExpired: boolean
}

let policyState: MfaPolicy = {
  enabled: true,
  defaultThreshold: '7d',
  rules: [
    {
      id: 'rule-sysadmin',
      targetType: 'role',
      targetName: 'SystemAdministrator',
      forceMfa: true,
      timeoutThreshold: 'always',
      description: 'System Administrators must re-verify MFA on every sign-in (always)',
    },
    {
      id: 'rule-engineer',
      targetType: 'role',
      targetName: 'Engineer',
      forceMfa: true,
      timeoutThreshold: '7d',
      description: 'Engineers must re-verify MFA once a week (7 days)',
    },
    {
      id: 'rule-technician',
      targetType: 'role',
      targetName: 'Technician',
      forceMfa: true,
      timeoutThreshold: '30d',
      description: 'Technicians must re-verify MFA once a month (30 days)',
    },
    {
      id: 'rule-maint-leads',
      targetType: 'group',
      targetName: 'Maintenance Leads',
      forceMfa: true,
      timeoutThreshold: '14d',
      description: 'Maintenance shift and group leaders must re-verify MFA bi-weekly (14 days)',
    },
  ],
}

export function getMfaPolicy(): MfaPolicy {
  return JSON.parse(JSON.stringify(policyState))
}

export function updateMfaPolicy(updated: Partial<MfaPolicy>): MfaPolicy {
  policyState = {
    ...policyState,
    ...updated,
    rules: updated.rules ? [...updated.rules] : policyState.rules,
  }
  return getMfaPolicy()
}

export function evaluateMfa(request: MfaEvaluationRequest): MfaEvaluationResult {
  const policy = policyState

  if (!policy.enabled) {
    return {
      mfaRequired: false,
      reason: 'Global MFA enforcement is currently disabled.',
      appliedThreshold: 'disabled',
      isExpired: false,
    }
  }

  const userRole = (request.role || '').trim().toLowerCase()
  const userGroups = (request.groups || []).map(g => g.trim().toLowerCase()).filter(Boolean)

  let matchedRule: MfaGroupRule | undefined

  // 1. Role match
  if (userRole) {
    matchedRule = policy.rules.find(
      r => r.targetType === 'role' && r.targetName.toLowerCase() === userRole && r.forceMfa,
    )
  }

  // 2. Group match
  if (!matchedRule && userGroups.length > 0) {
    matchedRule = policy.rules.find(
      r => r.targetType === 'group' && userGroups.includes(r.targetName.toLowerCase()) && r.forceMfa,
    )
  }

  const threshold = matchedRule?.timeoutThreshold || policy.defaultThreshold

  if (matchedRule && !matchedRule.forceMfa) {
    return {
      mfaRequired: false,
      reason: `MFA explicitly exempt for ${matchedRule.targetType} '${matchedRule.targetName}'.`,
      matchedRuleTarget: matchedRule.targetName,
      appliedThreshold: 'exempt',
      isExpired: false,
    }
  }

  if (threshold === 'always') {
    return {
      mfaRequired: true,
      reason: matchedRule
        ? `Policy requires MFA challenge on every sign-in ('always') for ${matchedRule.targetName}.`
        : 'Default policy requires MFA challenge on every sign-in.',
      matchedRuleTarget: matchedRule?.targetName,
      appliedThreshold: 'always',
      isExpired: true,
    }
  }

  if (threshold === 'never') {
    return {
      mfaRequired: false,
      reason: 'Policy threshold is configured to never require recurring MFA.',
      matchedRuleTarget: matchedRule?.targetName,
      appliedThreshold: 'never',
      isExpired: false,
    }
  }

  if (!request.lastMfaAt) {
    return {
      mfaRequired: true,
      reason: 'No previous MFA authentication timestamp recorded for session.',
      matchedRuleTarget: matchedRule?.targetName,
      appliedThreshold: threshold,
      isExpired: true,
    }
  }

  const minutesMap: Record<string, number> = {
    '12h': 12 * 60,
    '24h': 24 * 60,
    '7d': 7 * 24 * 60,
    '14d': 14 * 24 * 60,
    '30d': 30 * 24 * 60,
    '90d': 90 * 24 * 60,
  }

  let durationMinutes = minutesMap[threshold]
  if (!durationMinutes) {
    if (threshold === 'custom') {
      durationMinutes = (matchedRule?.customDays || 7) * 24 * 60
    } else {
      durationMinutes = 7 * 24 * 60
    }
  }

  const lastMfaTime = new Date(request.lastMfaAt).getTime()
  const expiresAtTime = lastMfaTime + durationMinutes * 60 * 1000
  const expiresAt = new Date(expiresAtTime).toISOString()
  const now = Date.now()
  const isExpired = now >= expiresAtTime

  const hoursRemaining = Math.max(0, Math.round(((expiresAtTime - now) / (1000 * 3600)) * 10) / 10)

  return {
    mfaRequired: isExpired,
    reason: isExpired
      ? `MFA session expired after ${threshold} threshold (expired at ${expiresAt}).`
      : `Valid MFA session active until ${expiresAt} (${hoursRemaining} hours remaining).`,
    matchedRuleTarget: matchedRule?.targetName,
    appliedThreshold: threshold,
    expiresAt,
    isExpired,
  }
}
