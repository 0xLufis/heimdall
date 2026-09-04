export interface AdCandidateHost {
  hostname: string
  name: string
  ipAddress: string
  macAddress: string
  osVersion: string
  machineIdentifier: string
  alreadyImported?: boolean
}

export interface AdOrganizationalUnit {
  ouPath: string
  name: string
  vlanId: number
  vlanName: string
  subnet: string
  location: string
  purpose: string
  machineType: string
  hostCount: number
  candidateHosts: AdCandidateHost[]
}

export interface TagTemplateRule {
  keyTemplate: string
  valueTemplate: string
}

export interface AdImportPreviewRequest {
  selectedOuPaths?: string[]
  namingPattern?: string
  tagTemplates?: Record<string, string>
  tagRules?: TagTemplateRule[]
}

export interface AdHostPreviewItem {
  hostname: string
  name: string
  macAddress: string
  ipAddress: string
  machineIdentifier: string
  osVersion: string
  vlanId: number
  vlanName: string
  subnet: string
  adOuPath: string
  ouTags: Record<string, string>
}

export interface AdHostImportRequest {
  hosts: AdHostPreviewItem[]
}

import { getPlantActiveDirectoryOUs, getPlantClientPcs } from './datasetLoader'

function buildActiveDirectoryOus(): AdOrganizationalUnit[] {
  try {
    const rawOus = getPlantActiveDirectoryOUs()
    const allPcs = getPlantClientPcs()
    return rawOus.map(ou => ({
      ouPath: ou.ouPath,
      name: ou.name,
      vlanId: ou.vlanId,
      vlanName: ou.vlanName,
      subnet: ou.subnet,
      location: ou.location,
      purpose: ou.purpose,
      machineType: ou.machineType,
      hostCount: ou.candidateHostnames.length,
      candidateHosts: ou.candidateHostnames.map(hn => {
        const pc = allPcs.find(p => p.hostname === hn)
        return {
          hostname: hn,
          name: pc?.name || hn,
          ipAddress: pc?.ipAddress || '10.0.0.1',
          macAddress: pc?.macAddress || '00:00:00:00:00:00',
          osVersion: pc?.osVersion || 'Windows 10 IoT Enterprise',
          machineIdentifier: pc?.machineIdentifier || `HW-${hn}`,
          alreadyImported: false
        }
      })
    }))
  } catch (err) {
    console.warn('[activeDirectoryStore] Error loading from datasetLoader, returning empty fallback:', err)
    return []
  }
}

const AD_OUS: AdOrganizationalUnit[] = buildActiveDirectoryOus()

export function getActiveDirectoryOus(): AdOrganizationalUnit[] {
  return JSON.parse(JSON.stringify(AD_OUS))
}

function extractOuTokens(ouPath: string): string[] {
  const matches = ouPath.match(/OU=([^,]+)/g) || []
  return matches.map(m => m.replace('OU=', ''))
}

export function resolvePattern(
  pattern: string,
  host: AdCandidateHost,
  ou: AdOrganizationalUnit,
): string {
  const ouTokens = extractOuTokens(ou.ouPath)
  let result = pattern
    .replace(/{HOSTNAME}/g, host.hostname)
    .replace(/{NAME}/g, host.name)
    .replace(/{IP}/g, host.ipAddress)
    .replace(/{MAC}/g, host.macAddress)
    .replace(/{VLAN_ID}/g, String(ou.vlanId))
    .replace(/{VLAN_NAME}/g, ou.vlanName)
    .replace(/{SUBNET}/g, ou.subnet)
    .replace(/{LOCATION}/g, ou.location)
    .replace(/{PURPOSE}/g, ou.purpose)
    .replace(/{MACHINE_TYPE}/g, ou.machineType)

  for (let i = 0; i < ouTokens.length; i++) {
    result = result.replace(new RegExp(`\\{OU\\[${i}\\]\\}`, 'g'), ouTokens[i])
  }

  return result
}

export function previewAdImport(request: AdImportPreviewRequest): {
  totalFound: number
  selectedOusCount: number
  preview: AdHostPreviewItem[]
} {
  const allOus = AD_OUS
  const selectedOus =
    request.selectedOuPaths && request.selectedOuPaths.length > 0
      ? allOus.filter(ou => request.selectedOuPaths!.includes(ou.ouPath))
      : allOus

  const tagTemplates = request.tagTemplates || {
    location: '{LOCATION}',
    purpose: '{PURPOSE}',
    machine_type: '{MACHINE_TYPE}',
    vlan: 'VLAN-{VLAN_ID}',
  }

  const preview: AdHostPreviewItem[] = []

  for (const ou of selectedOus) {
    for (const host of ou.candidateHosts) {
      const ouTags: Record<string, string> = {}
      if (request.tagRules && request.tagRules.length > 0) {
        for (const rule of request.tagRules) {
          if (!rule.keyTemplate) continue
          const key = resolvePattern(rule.keyTemplate, host, ou)
          const val = resolvePattern(rule.valueTemplate || '', host, ou)
          if (key) {
            ouTags[key] = val
          }
        }
      } else {
        for (const [tagKey, pattern] of Object.entries(tagTemplates)) {
          ouTags[tagKey] = resolvePattern(pattern, host, ou)
        }
      }

      preview.push({
        hostname: host.hostname,
        name: resolvePattern(request.namingPattern || '{NAME}', host, ou),
        macAddress: host.macAddress,
        ipAddress: host.ipAddress,
        machineIdentifier: host.machineIdentifier,
        osVersion: host.osVersion,
        vlanId: ou.vlanId,
        vlanName: ou.vlanName,
        subnet: ou.subnet,
        adOuPath: ou.ouPath,
        ouTags,
      })
    }
  }

  return {
    totalFound: preview.length,
    selectedOusCount: selectedOus.length,
    preview,
  }
}

// In-memory imported hosts store for BFF fallback
const importedHostsList: AdHostPreviewItem[] = []

export function commitImportedHosts(hosts: AdHostPreviewItem[]): {
  importedCount: number
  updatedCount: number
  totalProcessed: number
} {
  let importedCount = 0
  let updatedCount = 0

  for (const h of hosts) {
    const idx = importedHostsList.findIndex(x => x.macAddress === h.macAddress || x.hostname === h.hostname)
    if (idx >= 0) {
      importedHostsList[idx] = { ...h }
      updatedCount++
    } else {
      importedHostsList.push({ ...h })
      importedCount++
    }
  }

  return {
    importedCount,
    updatedCount,
    totalProcessed: hosts.length,
  }
}

export function getImportedAdHosts(): AdHostPreviewItem[] {
  return [...importedHostsList]
}
