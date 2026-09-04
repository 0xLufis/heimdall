import fs from 'node:fs'
import path from 'node:path'

export interface PlantMetadata {
  version: string
  plantName: string
  plantCode: string
  domain: string
  entraTenantId: string
  createdAt: string
  description: string
}

export interface PlantOrganization {
  id: string
  name: string
  slug: string
  description?: string
}

export interface PlantSecurityGroup {
  id: string
  groupIdentifier: string
  displayName: string
  identityProvider: string
  mappedRole: string
  mappedOrganizationName: string
  mappedOrgRole: string
  autoCreateOrg: boolean
  isEnabled: boolean
  memberUserIds: string[]
}

export interface PlantUser {
  id: string
  name: string
  email: string
  username: string
  primaryRole: string
  jobTitle: string
  department: string
  securityGroupIds: string[]
  presence: {
    availability: 'Available' | 'Busy' | 'OutOfOffice' | 'Away'
    isOutOfOffice: boolean
  }
  mfaPolicy?: string
  dedication?: {
    scopeType: 'technology' | 'group' | 'machine'
    targetId: string
    categoryFilter?: string
    role?: string
  }
}

export interface PlantCmiHardware {
  cpu: {
    Name: string
    NumberOfCores: number
    NumberOfLogicalProcessors: number
    MaxClockSpeed: number
  }
  memory: {
    Capacity: number
    Speed: number
    Manufacturer: string
    PartNumber: string
  }
  disks: Array<{
    Caption: string
    Size: number
    FreeSpace: number
    FileSystem: string
  }>
  bios: {
    Manufacturer: string
    SMBIOSBIOSVersion: string
    SerialNumber: string
  }
  computerSystem: {
    Name: string
    Manufacturer: string
    Model: string
    TotalPhysicalMemory: number
    Domain: string
  }
  network: {
    Description: string
    IPAddress: string
    MACAddress: string
    DefaultIPGateway: string
    DNSServerSearchOrder: string[]
  }
  os: {
    Caption: string
    Version: string
    BuildNumber: string
    OSArchitecture: string
    InstallDate: string
  }
}

export interface PlantClientPc {
  hostname: string
  name: string
  machineIdentifier: string
  ipAddress: string
  macAddress: string
  vlanId: number
  adOuPath: string
  machineType: string
  groupId?: string
  osVersion: string
  cmiHardware: PlantCmiHardware
  installedPackages: string[]
}

export interface PlantActiveDirectoryOU {
  ouPath: string
  name: string
  vlanId: number
  vlanName: string
  subnet: string
  location: string
  purpose: string
  machineType: string
  hostCount: number
  candidateHostnames: string[]
}

export interface PlantMachineGroup {
  id: string
  name: string
  description?: string
  parentId?: string | null
  machineIds: string[]
  machineTypes: string[]
  color?: string
}

export interface PlantTechnicianRule {
  id: string
  name: string
  technicianId: string
  technicianName: string
  technicianEmail?: string
  scopeType: 'technology' | 'group' | 'machine'
  targetId: string
  categoryFilter?: string
  backupTechnicianId?: string
  backupTechnicianName?: string
  assignedByRole?: 'shift_leader' | 'group_leader' | 'manager'
}

export interface EnterprisePlantDataset {
  metadata: PlantMetadata
  organizations: PlantOrganization[]
  securityGroups: PlantSecurityGroup[]
  users: PlantUser[]
  activeDirectoryOUs: PlantActiveDirectoryOU[]
  clientPcs: PlantClientPc[]
  machineGroups: PlantMachineGroup[]
  technicianRules: PlantTechnicianRule[]
  shiftAbsences: any[]
  mfaPolicies: Record<string, any>
}

let cachedDataset: EnterprisePlantDataset | null = null

function findDatasetFile(): string | null {
  const candidatePaths = [
    path.resolve(process.cwd(), 'fixtures/enterprise_plant_dataset.json'),
    path.resolve(process.cwd(), '../fixtures/enterprise_plant_dataset.json'),
    path.resolve(process.cwd(), '../../fixtures/enterprise_plant_dataset.json'),
    '/app/fixtures/enterprise_plant_dataset.json',
    path.resolve(import.meta.dirname || '', '../../fixtures/enterprise_plant_dataset.json')
  ]

  for (const p of candidatePaths) {
    if (fs.existsSync(p)) {
      return p
    }
  }
  return null
}

export function getEnterpriseDataset(): EnterprisePlantDataset {
  if (cachedDataset) return cachedDataset

  const filePath = findDatasetFile()
  if (filePath) {
    try {
      const raw = fs.readFileSync(filePath, 'utf-8')
      cachedDataset = JSON.parse(raw) as EnterprisePlantDataset
      return cachedDataset!
    } catch (err) {
      console.warn('[DatasetLoader] Error parsing dataset from file:', err)
    }
  }

  throw new Error('Could not locate fixtures/enterprise_plant_dataset.json')
}

export function getPlantMetadata(): PlantMetadata {
  return getEnterpriseDataset().metadata
}

export function getPlantOrganizations(): PlantOrganization[] {
  return getEnterpriseDataset().organizations
}

export function getPlantSecurityGroups(): PlantSecurityGroup[] {
  return getEnterpriseDataset().securityGroups
}

export function getPlantUsers(): PlantUser[] {
  return getEnterpriseDataset().users
}

export function getPlantClientPcs(): PlantClientPc[] {
  return getEnterpriseDataset().clientPcs
}

export function getPlantActiveDirectoryOUs(): PlantActiveDirectoryOU[] {
  return getEnterpriseDataset().activeDirectoryOUs
}

export function getPlantMachineGroups(): PlantMachineGroup[] {
  return getEnterpriseDataset().machineGroups
}

export function getPlantTechnicianRules(): PlantTechnicianRule[] {
  return getEnterpriseDataset().technicianRules
}
