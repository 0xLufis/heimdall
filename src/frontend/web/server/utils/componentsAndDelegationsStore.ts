import { randomUUID } from 'node:crypto'

/**
 * Process-local skills catalog & personnel certification store backing `server/api/skills/**`.
 *
 * NOTE: component recipes/slots/part history, delegation recipes/bindings, personnel groups and the
 * machine software/license/document/service-log quadrants used to live here as seeded in-memory
 * placeholders. Those BFF routes now call the .NET backend (Component/Delegation controllers) via
 * `backendFetch`, and the placeholder state was removed. Skills remain here until the skills routes
 * are migrated to the backend SkillsController.
 */

export interface SkillDefinitionEntry {
  id: string
  code: string
  name: string
  description?: string
  category: 'Certification' | 'TechnologyKnowHow' | 'MachineAuthorization' | 'SafetyClearance'
  issuer?: string
  validityMonths?: number
  createdAt: string
}

export interface PersonnelSkillEntry {
  id: string
  userId: string
  userName?: string
  skillId: string
  skillCode: string
  skillName: string
  category: string
  certifiedAt: string
  expiresAt?: string
  certificateNumber?: string
  verifiedBy?: string
}

const SEED_SKILLS: SkillDefinitionEntry[] = [
  {
    id: 'skill-laser-4',
    code: 'CERT-LASER-4',
    name: 'Laser Class 4 Safety Officer Certification',
    description: 'Certified laser safety officer authorized to service enclosed high-power fiber laser cells',
    category: 'SafetyClearance',
    issuer: 'TÜV Rheinland / DGUV',
    validityMonths: 24,
    createdAt: '2025-01-01T00:00:00Z'
  },
  {
    id: 'skill-twincat3',
    code: 'SKILL-TWINCAT3',
    name: 'Beckhoff TwinCAT 3 Motion Specialist',
    description: 'Authorized engineer for NC PTP axis configuration, kinematics, and structured text modification',
    category: 'TechnologyKnowHow',
    issuer: 'Beckhoff Automation Academy',
    validityMonths: 36,
    createdAt: '2025-01-01T00:00:00Z'
  },
  {
    id: 'skill-cognex-ai',
    code: 'CERT-COGNEX-DEV',
    name: 'Cognex VisionPro AI Developer',
    description: 'Deep learning vision model training, calibration, and classification tuning clearance',
    category: 'Certification',
    issuer: 'Cognex Corporation',
    validityMonths: 36,
    createdAt: '2025-01-01T00:00:00Z'
  },
  {
    id: 'skill-hv-safety',
    code: 'AUTH-HIGH-VOLT',
    name: 'High Voltage EV Battery Safety Clearance',
    description: 'Authorized qualification to disconnect, inspect, and service battery module electrical interconnects',
    category: 'SafetyClearance',
    issuer: 'DGUV-I 209-093',
    validityMonths: 12,
    createdAt: '2025-01-01T00:00:00Z'
  }
]

const SEED_PERSONNEL_SKILLS: PersonnelSkillEntry[] = [
  {
    id: 'ps-orwell-laser',
    userId: 'usr-orwell',
    userName: 'Engineer Orwell',
    skillId: 'skill-laser-4',
    skillCode: 'CERT-LASER-4',
    skillName: 'Laser Class 4 Safety Officer Certification',
    category: 'SafetyClearance',
    certifiedAt: '2025-04-10T00:00:00Z',
    expiresAt: '2027-04-10T00:00:00Z',
    certificateNumber: 'TUV-LAS-2025-9941',
    verifiedBy: 'Plant Safety Manager'
  },
  {
    id: 'ps-orwell-twincat',
    userId: 'usr-orwell',
    userName: 'Engineer Orwell',
    skillId: 'skill-twincat3',
    skillCode: 'SKILL-TWINCAT3',
    skillName: 'Beckhoff TwinCAT 3 Motion Specialist',
    category: 'TechnologyKnowHow',
    certifiedAt: '2024-11-01T00:00:00Z',
    expiresAt: '2027-11-01T00:00:00Z',
    certificateNumber: 'BECK-TC3-00412',
    verifiedBy: 'Lead Controls Engineer'
  },
  {
    id: 'ps-sally-cognex',
    userId: 'usr-sally',
    userName: 'Engineer Sally',
    skillId: 'skill-cognex-ai',
    skillCode: 'CERT-COGNEX-DEV',
    skillName: 'Cognex VisionPro AI Developer',
    category: 'Certification',
    certifiedAt: '2025-06-15T00:00:00Z',
    expiresAt: '2028-06-15T00:00:00Z',
    certificateNumber: 'COG-AI-8812',
    verifiedBy: 'Quality Director'
  },
  {
    id: 'ps-alex-hv',
    userId: 'usr-alex',
    userName: 'Technician Alex',
    skillId: 'skill-hv-safety',
    skillCode: 'AUTH-HIGH-VOLT',
    skillName: 'High Voltage EV Battery Safety Clearance',
    category: 'SafetyClearance',
    certifiedAt: '2026-01-10T00:00:00Z',
    expiresAt: '2027-01-10T00:00:00Z',
    certificateNumber: 'DGUV-HV-2026-019',
    verifiedBy: 'EHS Department'
  }
]


class ComponentsAndDelegationsStore {
  private skills: Map<string, SkillDefinitionEntry> = new Map()
  private personnelSkills: Map<string, PersonnelSkillEntry> = new Map()

  constructor() {
    this.reset()
  }

  public reset() {
    this.skills.clear()
    for (const sk of SEED_SKILLS) this.skills.set(sk.id, { ...sk })

    this.personnelSkills.clear()
    for (const ps of SEED_PERSONNEL_SKILLS) this.personnelSkills.set(ps.id, { ...ps })
  }

  // --- Skills Catalog & Personnel Certifications ---
  public getSkillsCatalog(): SkillDefinitionEntry[] {
    return Array.from(this.skills.values())
  }

  public createSkillDefinition(data: Omit<SkillDefinitionEntry, 'id' | 'createdAt'>): SkillDefinitionEntry {
    const id = `skill-${randomUUID().slice(0, 8)}`
    const entry: SkillDefinitionEntry = {
      id,
      ...data,
      code: data.code.toUpperCase().trim(),
      createdAt: new Date().toISOString()
    }
    this.skills.set(id, entry)
    return entry
  }

  public getPersonnelSkills(userId?: string): PersonnelSkillEntry[] {
    const all = Array.from(this.personnelSkills.values())
    if (!userId) return all
    return all.filter(ps => ps.userId === userId)
  }

  public assignSkillToUser(payload: {
    userId: string
    userName?: string
    skillId: string
    certificateNumber?: string
    verifiedBy?: string
    expiresAt?: string
  }): PersonnelSkillEntry {
    const skill = this.skills.get(payload.skillId)
    if (!skill) throw new Error(`Skill with ID "${payload.skillId}" not found.`)

    const existing = Array.from(this.personnelSkills.values()).find(
      ps => ps.userId === payload.userId && ps.skillId === payload.skillId
    )

    if (existing) {
      existing.certificateNumber = payload.certificateNumber || existing.certificateNumber
      existing.verifiedBy = payload.verifiedBy || existing.verifiedBy
      existing.expiresAt = payload.expiresAt || existing.expiresAt
      this.personnelSkills.set(existing.id, existing)
      return existing
    }

    const id = `ps-${randomUUID().slice(0, 8)}`
    const entry: PersonnelSkillEntry = {
      id,
      userId: payload.userId,
      userName: payload.userName,
      skillId: skill.id,
      skillCode: skill.code,
      skillName: skill.name,
      category: skill.category,
      certifiedAt: new Date().toISOString(),
      expiresAt: payload.expiresAt,
      certificateNumber: payload.certificateNumber,
      verifiedBy: payload.verifiedBy
    }
    this.personnelSkills.set(id, entry)
    return entry
  }

  public deleteSkillDefinition(id: string): boolean {
    return this.skills.delete(id)
  }

  public removeSkillFromUser(id: string): boolean {
    return this.personnelSkills.delete(id)
  }
}

export const componentsAndDelegationsStore = new ComponentsAndDelegationsStore()
