import { randomUUID } from 'node:crypto'
import type {
  ComponentRecipe,
  MachineComponentSlot,
  ComponentPartHistory,
  EplanCadReference
} from '../../app/types/inventory'
import type {
  TicketTelemetryProbe,
  ResponsibleDepartment
} from '../../app/types/maintenance'

export interface DelegationRecipeEntry {
  id: string
  name: string
  description?: string
  discipline: 'Mechanical' | 'Controls' | 'Electrical' | 'Safety' | 'Quality' | 'Process' | 'Tooling' | 'General'
  triggerEventCategory?: string
  allowedSchedules: Array<'Shift' | 'OfficeTime' | 'OnCall'>
  requiredSkillIds: string[]
  requiredSkillCodes: string[]
  primaryPersonnelGroupId?: string
  primaryPersonnelGroupName?: string
  primaryUserId?: string
  primaryUserName?: string
  onCallFallbackUserId?: string
  onCallFallbackUserName?: string
  escalationGroupId?: string
  escalationGroupName?: string
  escalationTimeoutMinutes: number
  isActive: boolean
  createdAt: string
}

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

export interface MachineDelegationBindingEntry {
  id: string
  machineId: string
  delegationRecipeId: string
  delegationRecipeName: string
  discipline: string
  isActive: boolean
}

export interface PersonnelGroupEntry {
  id: string
  name: string
  code: string
  groupType: 'ShiftCrew' | 'OfficeTimeStaff' | 'OnCallRoster' | 'SpecialistTeam'
  discipline: 'Mechanical' | 'Controls' | 'Electrical' | 'Safety' | 'Quality' | 'Process' | 'Tooling' | 'General'
  shiftPattern?: 'Morning' | 'Afternoon' | 'Night' | 'Office' | 'Weekend'
  leadUserId?: string
  leadUserName?: string
  memberUserIds: string[]
  memberUserNames: string[]
  description?: string
  createdAt: string
}

export interface SoftwareAssetEntry {
  id: string
  machineId: string
  name: string
  softwareType: 'Binary' | 'Runtime' | 'Config' | 'Program' | 'Recipe'
  version: string
  author?: string
  checksumSha256?: string
  status: 'Active' | 'Staged' | 'Deprecated' | 'Draft'
  description?: string
  payloadSummary?: string
  updatedAt: string
}

export interface LicenseAssetEntry {
  id: string
  machineId: string
  name: string
  licenseType: 'Dongle_HardwareLocked' | 'NodeLocked_MAC' | 'TwinCAT_SystemId' | 'FloatingServer'
  vendor: string
  seatsCount: number
  validFrom: string
  expiresAt?: string
  isPerpetual: boolean
  status: 'Active' | 'Expired' | 'GracePeriod' | 'Revoked'
  hardwareLockIdentifier?: string
}

export interface MachineDocumentEntry {
  id: string
  machineId: string
  title: string
  documentType: 'ElectricalSchematic' | 'PneumaticDiagram' | 'UserManual' | 'CeDeclaration' | 'SOP'
  documentNumber?: string
  revision?: string
  storagePath: string
  fileSizeBytes: number
  language: string
  uploadedAt: string
}

export interface MachineServiceLogEntry {
  id: string
  machineId: string
  timestamp: string
  author: string
  entryType: 'RoutineService' | 'ShiftHandover' | 'PartReplacement' | 'Calibration' | 'EmergencyRepair' | 'Note'
  summary: string
  details?: string
  linkedTicketId?: string
}

// ---------------------------------------------------------------------------
// Seed Data
// ---------------------------------------------------------------------------
const SEED_RECIPES: ComponentRecipe[] = [
  {
    id: 'rec-smc-valve-500',
    recipeCode: 'REC-SMC-VALVE-500',
    name: 'SMC Pneumatic Directional Valve 5/2',
    description: 'High-speed pilot operated pneumatic solenoid valve with integrated silencer',
    category: 'Hardware',
    manufacturer: 'SMC Pneumatics',
    modelNumber: 'SY5120-5LZD-01',
    baseCostEur: 185.0,
    autoTags: ['Pneumatic', 'Valve', 'SMC', 'SafetyCircuit'],
    eplanCadDefaults: {
      deviceTag: '-1V',
      drawingPage: 'Page 12.4',
      functionText: 'Directional Clamping Control'
    }
  },
  {
    id: 'rec-siemens-s120',
    recipeCode: 'REC-SIEMENS-S120',
    name: 'Siemens SINAMICS S120 Servo Drive',
    description: 'Modular high-performance positioning servo drive with PROFINET IRT',
    category: 'Hardware',
    manufacturer: 'Siemens',
    modelNumber: '6SL3120-1TE21-8AA3',
    baseCostEur: 1420.0,
    autoTags: ['Drive', 'Siemens', 'Profinet', 'Servo', 'MotionControl'],
    eplanCadDefaults: {
      deviceTag: '-10U1',
      drawingPage: 'Page 22.1',
      functionText: 'Spindle Servo Positioning Controller'
    }
  },
  {
    id: 'rec-cognex-is2800',
    recipeCode: 'REC-COGNEX-IS2800',
    name: 'Cognex In-Sight 2800 AI Vision Sensor',
    description: 'Deep learning classification sensor with integrated multicolor illumination',
    category: 'Hardware',
    manufacturer: 'Cognex',
    modelNumber: 'IS2801-100',
    baseCostEur: 2890.0,
    autoTags: ['Vision', 'Cognex', 'EthernetIP', 'Camera', 'QualityCheck'],
    eplanCadDefaults: {
      deviceTag: '-3B1',
      drawingPage: 'Page 34.2',
      functionText: 'Surface Defect Inspection Camera'
    }
  },
  {
    id: 'rec-trumpf-optic',
    recipeCode: 'REC-TRUMPF-OPTIC',
    name: 'Trumpf BEO D70 Laser Focusing Optic',
    description: 'Precision robotic laser welding processing head with cross-jet gas protection',
    category: 'Hardware',
    manufacturer: 'Trumpf',
    modelNumber: 'BEO-D70-WL',
    baseCostEur: 6800.0,
    autoTags: ['Laser', 'Trumpf', 'Optics', 'Class4', 'WeldingHead'],
    eplanCadDefaults: {
      deviceTag: '-OPT1',
      drawingPage: 'Page 18.5',
      functionText: 'Laser Focusing & Seam Tracker'
    }
  },
  {
    id: 'rec-weld-busbar-params',
    recipeCode: 'REC-WELD-BUSBAR-PARAMS',
    name: 'Aluminum Busbar Laser Welding Recipe v2.4',
    description: 'Calibrated laser power curve and wobble amplitude parameters for cell tab welding',
    category: 'SoftwareRecipe',
    manufacturer: 'In-House Welding Engineering',
    modelNumber: 'BUSBAR-WELD-PARAM-2026',
    baseCostEur: 0.0,
    autoTags: ['Recipe', 'Welding', 'Parameters', 'Quality', 'LaserWeld']
  }
]

const SEED_SLOTS: MachineComponentSlot[] = [
  // LINE1-OP10
  {
    id: 'slot-op10-valve',
    machineId: '11111111-1111-1111-1111-111111111111',
    name: 'Main Infeed Clamping Valve',
    functionalSlotIdentifier: '=LINE1+OP10-1V1',
    category: 'Hardware',
    componentRecipeId: 'rec-smc-valve-500',
    componentRecipeCode: 'REC-SMC-VALVE-500',
    currentPartId: 'part-smc-01',
    currentPartSerialNumber: 'SN-SMC-88124',
    tags: ['Pneumatic', 'Valve', 'SMC', 'SafetyCircuit', 'InfeedClamp'],
    eplanCadRef: {
      deviceTag: '-1V1',
      drawingPage: 'Page 12.4',
      cadHandle: '2A1B',
      bomPosition: 'Pos 04.1',
      functionText: 'Raw Material Gripper Clamp'
    },
    installedAt: '2026-02-10T08:00:00Z',
    updatedAt: '2026-02-10T08:00:00Z'
  },
  {
    id: 'slot-op10-motor',
    machineId: '11111111-1111-1111-1111-111111111111',
    name: 'Infeed Conveyor Servo Motor Drive',
    functionalSlotIdentifier: '=LINE1+OP10-10U1',
    category: 'Hardware',
    componentRecipeId: 'rec-siemens-s120',
    componentRecipeCode: 'REC-SIEMENS-S120',
    currentPartId: 'part-siemens-01',
    currentPartSerialNumber: 'SN-SIE-40912',
    tags: ['Drive', 'Siemens', 'Profinet', 'Servo', 'MotionControl'],
    eplanCadRef: {
      deviceTag: '-10U1',
      drawingPage: 'Page 22.1',
      cadHandle: '3C4D',
      bomPosition: 'Pos 12.2',
      functionText: 'Infeed Indexing Drive'
    },
    installedAt: '2026-01-15T09:30:00Z',
    updatedAt: '2026-01-15T09:30:00Z'
  },
  // LINE1-OP20
  {
    id: 'slot-op20-optic',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Trumpf Laser Welding Processing Head',
    functionalSlotIdentifier: '=LINE1+OP20-OPT1',
    category: 'Hardware',
    componentRecipeId: 'rec-trumpf-optic',
    componentRecipeCode: 'REC-TRUMPF-OPTIC',
    currentPartId: 'part-trumpf-912',
    currentPartSerialNumber: 'SN-TRUMPF-OPT-912',
    tags: ['Laser', 'Trumpf', 'Optics', 'Class4', 'WeldingHead', 'CriticalSpares'],
    eplanCadRef: {
      deviceTag: '-OPT1',
      drawingPage: 'Page 18.5',
      cadHandle: '7E8F',
      bomPosition: 'Pos 01.1',
      functionText: 'Laser Focusing & Seam Tracker'
    },
    installedAt: '2026-09-02T14:15:00Z',
    updatedAt: '2026-09-02T14:15:00Z'
  },
  {
    id: 'slot-op20-recipe',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Busbar Welding Process Recipe Slot',
    functionalSlotIdentifier: '=LINE1+OP20-REC01',
    category: 'SoftwareRecipe',
    componentRecipeId: 'rec-weld-busbar-params',
    componentRecipeCode: 'REC-WELD-BUSBAR-PARAMS',
    tags: ['Recipe', 'Welding', 'Parameters', 'Quality', 'LaserWeld'],
    installedAt: '2026-08-20T11:00:00Z',
    updatedAt: '2026-08-20T11:00:00Z'
  },
  // LINE2-OP30
  {
    id: 'slot-op30-cam',
    machineId: '33333333-3333-3333-3333-333333333333',
    name: 'Cognex Top Surface Inspection Sensor',
    functionalSlotIdentifier: '=LINE2+OP30-3B1',
    category: 'Hardware',
    componentRecipeId: 'rec-cognex-is2800',
    componentRecipeCode: 'REC-COGNEX-IS2800',
    currentPartId: 'part-cognex-01',
    currentPartSerialNumber: 'SN-COG-77411',
    tags: ['Vision', 'Cognex', 'EthernetIP', 'Camera', 'QualityCheck'],
    eplanCadRef: {
      deviceTag: '-3B1',
      drawingPage: 'Page 34.2',
      cadHandle: '9A0B',
      bomPosition: 'Pos 02.4',
      functionText: 'Surface Defect Inspection Camera'
    },
    installedAt: '2026-03-01T10:00:00Z',
    updatedAt: '2026-03-01T10:00:00Z'
  }
]

const SEED_PART_HISTORY: ComponentPartHistory[] = [
  {
    id: 'hist-op20-optic-01',
    componentId: 'slot-op20-optic',
    machineId: '22222222-2222-2222-2222-222222222222',
    swappedAt: '2026-09-02T14:15:00Z',
    swappedByUserId: 'usr-orwell',
    swappedByUserName: 'Engineer Orwell',
    previousPartId: 'part-trumpf-881',
    previousPartSerialNumber: 'SN-TRUMPF-OPT-881',
    newPartId: 'part-trumpf-912',
    newPartSerialNumber: 'SN-TRUMPF-OPT-912',
    reason: 'WearDepreciation',
    previousPartDisposition: 'SentForRepair',
    notes: 'Beam distortion and spatter contamination on internal collimating lens group. Sent to Trumpf Ditzingen for optical recoating.'
  }
]

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

const SEED_DELEGATION_RECIPES: DelegationRecipeEntry[] = [
  {
    id: 'deleg-laser-safety',
    name: 'Laser Safety & Optical Response Recipe',
    description: 'Immediate response protocol for optical degradation, beam misalignment, and safety enclosure alarms',
    discipline: 'Safety',
    triggerEventCategory: 'OpticalAlarm',
    allowedSchedules: ['Shift', 'OfficeTime', 'OnCall'],
    requiredSkillIds: ['skill-laser-4'],
    requiredSkillCodes: ['CERT-LASER-4'],
    primaryPersonnelGroupId: 'grp-laser-safety',
    primaryPersonnelGroupName: 'Laser Safety Specialist Crew',
    primaryUserId: 'usr-orwell',
    primaryUserName: 'Engineer Orwell',
    onCallFallbackUserId: 'usr-dr-standby',
    onCallFallbackUserName: 'Dr. Standby Engineer',
    escalationTimeoutMinutes: 20,
    isActive: true,
    createdAt: '2026-01-10T00:00:00Z'
  },
  {
    id: 'deleg-controls-plc',
    name: 'Controls & PLC Diagnostics Recipe',
    description: 'Automated dispatch for fieldbus faults, servo over-temperature, and sync cycle timeouts',
    discipline: 'Controls',
    triggerEventCategory: 'PlcError',
    allowedSchedules: ['Shift', 'OfficeTime', 'OnCall'],
    requiredSkillIds: ['skill-twincat3'],
    requiredSkillCodes: ['SKILL-TWINCAT3'],
    primaryPersonnelGroupId: 'grp-controls-crew',
    primaryPersonnelGroupName: 'Plant Controls & Automation Group',
    primaryUserId: 'usr-orwell',
    primaryUserName: 'Engineer Orwell',
    onCallFallbackUserId: 'usr-dr-standby',
    onCallFallbackUserName: 'Dr. Standby Engineer',
    escalationTimeoutMinutes: 30,
    isActive: true,
    createdAt: '2026-01-10T00:00:00Z'
  },
  {
    id: 'deleg-mech-pm',
    name: 'Mechanical Preventive Maintenance Recipe',
    description: 'Routing for pneumatic pressure drop, clamping jams, and conveyor motor maintenance',
    discipline: 'Mechanical',
    triggerEventCategory: 'MechanicalJam',
    allowedSchedules: ['Shift', 'OfficeTime'],
    requiredSkillIds: [],
    requiredSkillCodes: [],
    primaryPersonnelGroupId: 'grp-mech-maint',
    primaryPersonnelGroupName: 'Mechanical Maintenance Team',
    primaryUserId: 'usr-alex',
    primaryUserName: 'Technician Alex',
    escalationTimeoutMinutes: 45,
    isActive: true,
    createdAt: '2026-01-10T00:00:00Z'
  },
  {
    id: 'deleg-vision-qa',
    name: 'Vision & AI Quality Inspection Recipe',
    description: 'Routing for camera occlusion, false reject rate spikes, and illumination drift',
    discipline: 'Quality',
    triggerEventCategory: 'VisionFault',
    allowedSchedules: ['Shift', 'OfficeTime'],
    requiredSkillIds: ['skill-cognex-ai'],
    requiredSkillCodes: ['CERT-COGNEX-DEV'],
    primaryPersonnelGroupId: 'grp-quality-auto',
    primaryPersonnelGroupName: 'Quality Automation Team',
    primaryUserId: 'usr-sally',
    primaryUserName: 'Engineer Sally',
    escalationTimeoutMinutes: 30,
    isActive: true,
    createdAt: '2026-01-10T00:00:00Z'
  }
]

const SEED_PERSONNEL_GROUPS: PersonnelGroupEntry[] = [
  {
    id: 'grp-laser-safety',
    name: 'Laser Safety Specialist Crew',
    code: 'GRP-LSR-SAFE',
    groupType: 'SpecialistTeam',
    discipline: 'Safety',
    shiftPattern: 'Morning',
    leadUserId: 'usr-orwell',
    leadUserName: 'Engineer Orwell',
    memberUserIds: ['usr-orwell', 'usr-alex'],
    memberUserNames: ['Engineer Orwell', 'Technician Alex'],
    description: 'Certified Class 4 Laser Safety Specialists responsible for optical path maintenance',
    createdAt: '2025-01-15T08:00:00Z'
  },
  {
    id: 'grp-controls-crew',
    name: 'Plant Controls & Automation Group',
    code: 'GRP-CTRL-AUTO',
    groupType: 'ShiftCrew',
    discipline: 'Controls',
    shiftPattern: 'Morning',
    leadUserId: 'usr-orwell',
    leadUserName: 'Engineer Orwell',
    memberUserIds: ['usr-orwell', 'usr-senior-eng-1', 'tech-03'],
    memberUserNames: ['Engineer Orwell', 'Elena Rostova', 'Szabó Tamás'],
    description: 'Beckhoff TwinCAT, Siemens TIA Portal, and EtherCAT fieldbus response team',
    createdAt: '2025-01-15T08:00:00Z'
  },
  {
    id: 'grp-mech-maint',
    name: 'Mechanical Maintenance Team',
    code: 'GRP-MECH-MAINT',
    groupType: 'ShiftCrew',
    discipline: 'Mechanical',
    shiftPattern: 'Morning',
    leadUserId: 'usr-alex',
    leadUserName: 'Technician Alex',
    memberUserIds: ['usr-alex', 'usr-tech-welding-1'],
    memberUserNames: ['Technician Alex', 'István Kovács'],
    description: 'Actuators, pneumatics, hydraulic clamping, and mechanical drive maintenance',
    createdAt: '2025-01-15T08:00:00Z'
  },
  {
    id: 'grp-quality-auto',
    name: 'Quality Automation Team',
    code: 'GRP-QUAL-AUTO',
    groupType: 'OfficeTimeStaff',
    discipline: 'Quality',
    shiftPattern: 'Office',
    leadUserId: 'usr-sally',
    leadUserName: 'Engineer Sally',
    memberUserIds: ['usr-sally', 'usr-katalin'],
    memberUserNames: ['Engineer Sally', 'Katalin Nagy'],
    description: 'Vision inspection algorithm tuning, metrology, and AOI quality validation',
    createdAt: '2025-01-15T08:00:00Z'
  },
  {
    id: 'grp-oncall-standby',
    name: 'Tier-3 Engineering On-Call Standby',
    code: 'GRP-ONCALL-T3',
    groupType: 'OnCallRoster',
    discipline: 'General',
    shiftPattern: 'Weekend',
    leadUserId: 'usr-dr-standby',
    leadUserName: 'Dr. Standby Engineer',
    memberUserIds: ['usr-dr-standby', 'usr-director-1'],
    memberUserNames: ['Dr. Standby Engineer', 'Dr. Henrik Weber'],
    description: 'Off-hours emergency fallback roster for line-stop level industrial incidents',
    createdAt: '2025-01-15T08:00:00Z'
  }
]

const SEED_DELEGATION_BINDINGS: MachineDelegationBindingEntry[] = [
  // OP20 has both Laser Safety and Controls bindings!
  {
    id: 'bind-op20-laser',
    machineId: '22222222-2222-2222-2222-222222222222',
    delegationRecipeId: 'deleg-laser-safety',
    delegationRecipeName: 'Laser Safety & Optical Response Recipe',
    discipline: 'Safety',
    isActive: true
  },
  {
    id: 'bind-op20-controls',
    machineId: '22222222-2222-2222-2222-222222222222',
    delegationRecipeId: 'deleg-controls-plc',
    delegationRecipeName: 'Controls & PLC Diagnostics Recipe',
    discipline: 'Controls',
    isActive: true
  },
  // OP10 has Mechanical PM binding
  {
    id: 'bind-op10-mech',
    machineId: '11111111-1111-1111-1111-111111111111',
    delegationRecipeId: 'deleg-mech-pm',
    delegationRecipeName: 'Mechanical Preventive Maintenance Recipe',
    discipline: 'Mechanical',
    isActive: true
  },
  // OP30 has Vision QA binding
  {
    id: 'bind-op30-vision',
    machineId: '33333333-3333-3333-3333-333333333333',
    delegationRecipeId: 'deleg-vision-qa',
    delegationRecipeName: 'Vision & AI Quality Inspection Recipe',
    discipline: 'Quality',
    isActive: true
  }
]

const SEED_SOFTWARE_ASSETS: SoftwareAssetEntry[] = [
  {
    id: 'sw-op20-plc-prog',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'TwinCAT 3 Station Logic (Structured Text)',
    softwareType: 'Program',
    version: 'v4.12.0-rc2',
    author: 'Engineer Orwell',
    checksumSha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
    status: 'Active',
    description: 'Master IEC 61131-3 logic managing pneumatic clamping, safety gates, and seam tracking',
    payloadSummary: 'Compiled TwinCAT Boot Project (.app)',
    updatedAt: '2026-09-15T12:00:00Z'
  },
  {
    id: 'sw-op20-runtime',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Beckhoff TwinCAT 3 eXtended Automation Runtime (XAR)',
    softwareType: 'Runtime',
    version: '3.1.4024.47 (64-bit Real-time Kernel)',
    author: 'Beckhoff Automation',
    status: 'Active',
    description: 'Hard real-time deterministic kernel running on IPC-L1-02 core 3',
    updatedAt: '2026-01-10T08:00:00Z'
  },
  {
    id: 'sw-op20-weld-binary',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Trumpf TruControl Real-Time Laser Driver Binary',
    softwareType: 'Binary',
    version: 'v8.2.1-linux-x86_64',
    author: 'Trumpf System Engineering',
    checksumSha256: 'a1b2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef0123456789abcdef0',
    status: 'Active',
    description: 'Direct FPGA communication daemon for high-speed laser pulse triggering',
    updatedAt: '2026-04-12T16:30:00Z'
  },
  {
    id: 'sw-op20-config',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Profinet IO Station Configuration (GSDML)',
    softwareType: 'Config',
    version: 'rev2026.3',
    author: 'Siemens TIA Portal v19 Export',
    status: 'Active',
    description: 'Bus address allocation, cycle time tolerances (1.0ms), and safety telegram mappings',
    updatedAt: '2026-05-18T10:00:00Z'
  },
  {
    id: 'sw-op20-recipe-data',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Aluminum Busbar Laser Welding Recipe v2.4',
    softwareType: 'Recipe',
    version: 'v2.4',
    author: 'Welding Quality Laboratory',
    checksumSha256: '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08',
    status: 'Active',
    description: 'Process parameters: 4200W continuous wave, 140Hz wobble circular, 35mm/s robot travel',
    updatedAt: '2026-08-20T11:00:00Z'
  }
]

const SEED_LICENSES: LicenseAssetEntry[] = [
  {
    id: 'lic-op20-twincat',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'TwinCAT 3 NC PTP & Kinematics License',
    licenseType: 'TwinCAT_SystemId',
    vendor: 'Beckhoff Automation GmbH',
    seatsCount: 1,
    validFrom: '2026-01-01T00:00:00Z',
    isPerpetual: true,
    status: 'Active',
    hardwareLockIdentifier: 'TC-SYSID-HU-8912-7714'
  },
  {
    id: 'lic-op20-trumpf',
    machineId: '22222222-2222-2222-2222-222222222222',
    name: 'Trumpf SeamLine Real-time Tracking Feature License',
    licenseType: 'Dongle_HardwareLocked',
    vendor: 'Trumpf Laser SE',
    seatsCount: 1,
    validFrom: '2026-01-01T00:00:00Z',
    expiresAt: '2027-12-31T23:59:59Z',
    isPerpetual: false,
    status: 'Active',
    hardwareLockIdentifier: 'HASP-DONGLE-SN-49812'
  }
]

const SEED_DOCUMENTS: MachineDocumentEntry[] = [
  {
    id: 'doc-op20-eplan',
    machineId: '22222222-2222-2222-2222-222222222222',
    title: 'EPLAN Pro Panel Circuit Diagram & Cabinet Layout',
    documentType: 'ElectricalSchematic',
    documentNumber: 'EPLAN-LINE1-OP20-ELEC-V3',
    revision: 'Rev 3.2',
    storagePath: '/docs/schematics/LINE1-OP20-ELEC-V3.pdf',
    fileSizeBytes: 14850240,
    language: 'en',
    uploadedAt: '2026-02-01T10:00:00Z'
  },
  {
    id: 'doc-op20-pneumatic',
    machineId: '22222222-2222-2222-2222-222222222222',
    title: 'Pneumatic System Flow & Valve Manifold Diagram',
    documentType: 'PneumaticDiagram',
    documentNumber: 'PNEUM-OP20-F01',
    revision: 'Rev 1.0',
    storagePath: '/docs/schematics/LINE1-OP20-PNEUM.pdf',
    fileSizeBytes: 4210000,
    language: 'en',
    uploadedAt: '2026-02-05T14:00:00Z'
  },
  {
    id: 'doc-op20-ce',
    machineId: '22222222-2222-2222-2222-222222222222',
    title: 'EU Machinery Directive CE Declaration of Conformity',
    documentType: 'CeDeclaration',
    documentNumber: 'CE-DECL-2026-L1-020',
    revision: 'Final',
    storagePath: '/docs/compliance/CE-LINE1-OP20.pdf',
    fileSizeBytes: 1205000,
    language: 'en',
    uploadedAt: '2026-01-20T09:00:00Z'
  }
]

const SEED_SERVICE_LOGS: MachineServiceLogEntry[] = [
  {
    id: 'log-op20-01',
    machineId: '22222222-2222-2222-2222-222222222222',
    timestamp: '2026-09-02T14:20:00Z',
    author: 'Engineer Orwell',
    entryType: 'PartReplacement',
    summary: 'Optical focusing processing head replaced on slot =LINE1+OP20-OPT1',
    details: 'Old part SN-TRUMPF-OPT-881 removed due to lens spatter. New part SN-TRUMPF-OPT-912 installed and beam center aligned via test target.'
  },
  {
    id: 'log-op20-02',
    machineId: '22222222-2222-2222-2222-222222222222',
    timestamp: '2026-09-15T06:00:00Z',
    author: 'Shift Leader Viktor',
    entryType: 'ShiftHandover',
    summary: 'Morning shift handover — Cell OP20 operating nominal at 98.4% availability',
    details: 'Daily cross-jet gas pressure verified at 6.2 bar. Protective glass clean.'
  }
]

// ---------------------------------------------------------------------------
// Store Implementation
// ---------------------------------------------------------------------------
class ComponentsAndDelegationsStore {
  private recipes: Map<string, ComponentRecipe> = new Map()
  private slots: Map<string, MachineComponentSlot> = new Map()
  private partHistory: Map<string, ComponentPartHistory[]> = new Map()
  private skills: Map<string, SkillDefinitionEntry> = new Map()
  private personnelSkills: Map<string, PersonnelSkillEntry> = new Map()
  private personnelGroups: Map<string, PersonnelGroupEntry> = new Map()
  private delegationRecipes: Map<string, DelegationRecipeEntry> = new Map()
  private delegationBindings: Map<string, MachineDelegationBindingEntry[]> = new Map()
  private softwareAssets: Map<string, SoftwareAssetEntry[]> = new Map()
  private licenseAssets: Map<string, LicenseAssetEntry[]> = new Map()
  private documents: Map<string, MachineDocumentEntry[]> = new Map()
  private serviceLogs: Map<string, MachineServiceLogEntry[]> = new Map()

  constructor() {
    this.reset()
  }

  public reset() {
    this.recipes.clear()
    for (const r of SEED_RECIPES) this.recipes.set(r.id, { ...r })

    this.slots.clear()
    for (const s of SEED_SLOTS) this.slots.set(s.id, { ...s })

    this.partHistory.clear()
    for (const h of SEED_PART_HISTORY) {
      const list = this.partHistory.get(h.componentId) || []
      list.push({ ...h })
      this.partHistory.set(h.componentId, list)
    }

    this.skills.clear()
    for (const sk of SEED_SKILLS) this.skills.set(sk.id, { ...sk })

    this.personnelSkills.clear()
    for (const ps of SEED_PERSONNEL_SKILLS) this.personnelSkills.set(ps.id, { ...ps })

    this.personnelGroups.clear()
    for (const pg of SEED_PERSONNEL_GROUPS) this.personnelGroups.set(pg.id, { ...pg })

    this.delegationRecipes.clear()
    for (const dr of SEED_DELEGATION_RECIPES) this.delegationRecipes.set(dr.id, { ...dr })

    this.delegationBindings.clear()
    for (const b of SEED_DELEGATION_BINDINGS) {
      const list = this.delegationBindings.get(b.machineId) || []
      list.push({ ...b })
      this.delegationBindings.set(b.machineId, list)
    }

    this.softwareAssets.clear()
    for (const sw of SEED_SOFTWARE_ASSETS) {
      const list = this.softwareAssets.get(sw.machineId) || []
      list.push({ ...sw })
      this.softwareAssets.set(sw.machineId, list)
    }

    this.licenseAssets.clear()
    for (const lic of SEED_LICENSES) {
      const list = this.licenseAssets.get(lic.machineId) || []
      list.push({ ...lic })
      this.licenseAssets.set(lic.machineId, list)
    }

    this.documents.clear()
    for (const doc of SEED_DOCUMENTS) {
      const list = this.documents.get(doc.machineId) || []
      list.push({ ...doc })
      this.documents.set(doc.machineId, list)
    }

    this.serviceLogs.clear()
    for (const log of SEED_SERVICE_LOGS) {
      const list = this.serviceLogs.get(log.machineId) || []
      list.push({ ...log })
      this.serviceLogs.set(log.machineId, list)
    }
  }

  // --- Recipes ---
  public getRecipes(): ComponentRecipe[] {
    return Array.from(this.recipes.values())
  }

  public getRecipeById(id: string): ComponentRecipe | undefined {
    return this.recipes.get(id) || Array.from(this.recipes.values()).find(r => r.recipeCode === id)
  }

  public createRecipe(data: Omit<ComponentRecipe, 'id'>): ComponentRecipe {
    const id = `rec-${randomUUID().slice(0, 8)}`
    const recipe: ComponentRecipe = {
      id,
      ...data,
      autoTags: data.autoTags || [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
    this.recipes.set(id, recipe)
    return recipe
  }

  // --- Slots & Components ---
  public getSlotsByMachineId(machineId: string): MachineComponentSlot[] {
    return Array.from(this.slots.values()).filter(s => s.machineId === machineId)
  }

  public getSlotById(id: string): MachineComponentSlot | undefined {
    return this.slots.get(id)
  }

  public getPartHistory(componentId: string): ComponentPartHistory[] {
    return (this.partHistory.get(componentId) || []).sort(
      (a, b) => new Date(b.swappedAt).getTime() - new Date(a.swappedAt).getTime()
    )
  }

  public assignPart(payload: {
    machineId: string
    existingComponentId?: string
    newComponentSlotIdentifier?: string
    newComponentName?: string
    category?: any
    componentRecipeId?: string
    partId: string
    partSerialNumber?: string
    partName?: string
    swappedByUserId: string
    swappedByUserName?: string
    reason: any
    previousPartDisposition: any
    associatedTicketId?: string
    notes?: string
    eplanCadRef?: EplanCadReference
  }): {
    success: boolean
    componentId: string
    historyRecordId: string
    isNewSlot: boolean
  } {
    let targetSlot: MachineComponentSlot
    let isNewSlot = false
    const now = new Date().toISOString()

    if (payload.existingComponentId && this.slots.has(payload.existingComponentId)) {
      targetSlot = this.slots.get(payload.existingComponentId)!
      const previousPartId = targetSlot.currentPartId
      const previousPartSerial = targetSlot.currentPartSerialNumber

      // Record swap history
      const historyId = `hist-${randomUUID().slice(0, 8)}`
      const historyEntry: ComponentPartHistory = {
        id: historyId,
        componentId: targetSlot.id,
        machineId: payload.machineId,
        swappedAt: now,
        swappedByUserId: payload.swappedByUserId,
        swappedByUserName: payload.swappedByUserName,
        previousPartId,
        previousPartSerialNumber: previousPartSerial,
        newPartId: payload.partId,
        newPartSerialNumber: payload.partSerialNumber,
        reason: payload.reason,
        previousPartDisposition: payload.previousPartDisposition,
        associatedTicketId: payload.associatedTicketId,
        notes: payload.notes
      }

      const list = this.partHistory.get(targetSlot.id) || []
      list.push(historyEntry)
      this.partHistory.set(targetSlot.id, list)

      // Update slot
      targetSlot.currentPartId = payload.partId
      targetSlot.currentPartSerialNumber = payload.partSerialNumber
      targetSlot.updatedAt = now
      if (payload.eplanCadRef) targetSlot.eplanCadRef = payload.eplanCadRef
      this.slots.set(targetSlot.id, targetSlot)

      // Record Service Log
      this.addServiceLog({
        machineId: payload.machineId,
        author: payload.swappedByUserName || payload.swappedByUserId,
        entryType: 'PartReplacement',
        summary: `Part replaced on slot "${targetSlot.functionalSlotIdentifier}" (${payload.partSerialNumber || payload.partId})`,
        details: payload.notes,
        linkedTicketId: payload.associatedTicketId
      })

      return {
        success: true,
        componentId: targetSlot.id,
        historyRecordId: historyId,
        isNewSlot: false
      }
    } else {
      isNewSlot = true
      const slotId = `slot-${randomUUID().slice(0, 8)}`
      const tags: string[] = []

      let recipeCode: string | undefined
      if (payload.componentRecipeId) {
        const recipe = this.recipes.get(payload.componentRecipeId)
        if (recipe) {
          recipeCode = recipe.recipeCode
          if (recipe.autoTags) tags.push(...recipe.autoTags)
        }
      }

      targetSlot = {
        id: slotId,
        machineId: payload.machineId,
        name: payload.newComponentName || payload.partName || 'New Component Slot',
        functionalSlotIdentifier: payload.newComponentSlotIdentifier || `SLOT-${slotId.slice(-4).toUpperCase()}`,
        category: payload.category || 'Hardware',
        componentRecipeId: payload.componentRecipeId,
        componentRecipeCode: recipeCode,
        currentPartId: payload.partId,
        currentPartSerialNumber: payload.partSerialNumber,
        tags: Array.from(new Set(tags)),
        eplanCadRef: payload.eplanCadRef,
        installedAt: now,
        updatedAt: now
      }
      this.slots.set(slotId, targetSlot)

      // Initial history entry
      const historyId = `hist-${randomUUID().slice(0, 8)}`
      const historyEntry: ComponentPartHistory = {
        id: historyId,
        componentId: slotId,
        machineId: payload.machineId,
        swappedAt: now,
        swappedByUserId: payload.swappedByUserId,
        swappedByUserName: payload.swappedByUserName,
        previousPartId: undefined,
        previousPartSerialNumber: undefined,
        newPartId: payload.partId,
        newPartSerialNumber: payload.partSerialNumber,
        reason: 'InitialCommissioning',
        previousPartDisposition: 'RetainedForAnalysis',
        associatedTicketId: payload.associatedTicketId,
        notes: payload.notes || 'Initial slot commissioning'
      }
      this.partHistory.set(slotId, [historyEntry])

      // Record Service Log
      this.addServiceLog({
        machineId: payload.machineId,
        author: payload.swappedByUserName || payload.swappedByUserId,
        entryType: 'PartReplacement',
        summary: `New slot "${targetSlot.functionalSlotIdentifier}" commissioned with part ${payload.partSerialNumber || payload.partId}`,
        details: payload.notes,
        linkedTicketId: payload.associatedTicketId
      })

      return {
        success: true,
        componentId: slotId,
        historyRecordId: historyId,
        isNewSlot: true
      }
    }
  }

  // --- Delegation Recipes & Bindings ---
  public getDelegationRecipes(): DelegationRecipeEntry[] {
    return Array.from(this.delegationRecipes.values())
  }

  public getDelegationRecipeById(id: string): DelegationRecipeEntry | undefined {
    return this.delegationRecipes.get(id)
  }

  public createDelegationRecipe(data: Omit<DelegationRecipeEntry, 'id' | 'createdAt'>): DelegationRecipeEntry {
    const id = `deleg-${randomUUID().slice(0, 8)}`
    const entry: DelegationRecipeEntry = {
      id,
      ...data,
      createdAt: new Date().toISOString()
    }
    this.delegationRecipes.set(id, entry)
    return entry
  }

  public deleteDelegationRecipe(id: string): boolean {
    return this.delegationRecipes.delete(id)
  }

  // --- Personnel Groups ---
  public getPersonnelGroups(): PersonnelGroupEntry[] {
    return Array.from(this.personnelGroups.values())
  }

  public getPersonnelGroupById(id: string): PersonnelGroupEntry | undefined {
    return this.personnelGroups.get(id)
  }

  public createPersonnelGroup(data: Omit<PersonnelGroupEntry, 'id' | 'createdAt'>): PersonnelGroupEntry {
    const id = `grp-${randomUUID().slice(0, 8)}`
    const entry: PersonnelGroupEntry = {
      id,
      ...data,
      code: (data.code || `GRP-${data.name.substring(0, 4).toUpperCase()}`).trim(),
      createdAt: new Date().toISOString()
    }
    this.personnelGroups.set(id, entry)
    return entry
  }

  public deletePersonnelGroup(id: string): boolean {
    return this.personnelGroups.delete(id)
  }

  public getBindingsByMachineId(machineId: string): MachineDelegationBindingEntry[] {
    return this.delegationBindings.get(machineId) || []
  }

  public bindRecipeToMachine(machineId: string, recipeId: string, discipline: string): MachineDelegationBindingEntry {
    const recipe = this.delegationRecipes.get(recipeId)
    const list = this.delegationBindings.get(machineId) || []

    const existing = list.find(b => b.delegationRecipeId === recipeId && b.discipline === discipline)
    if (existing) {
      existing.isActive = true
      return existing
    }

    const binding: MachineDelegationBindingEntry = {
      id: `bind-${randomUUID().slice(0, 8)}`,
      machineId,
      delegationRecipeId: recipeId,
      delegationRecipeName: recipe?.name || 'Delegation Recipe',
      discipline,
      isActive: true
    }
    list.push(binding)
    this.delegationBindings.set(machineId, list)
    return binding
  }

  public unbindRecipe(machineId: string, bindingId: string): boolean {
    const list = this.delegationBindings.get(machineId) || []
    const idx = list.findIndex(b => b.id === bindingId)
    if (idx !== -1) {
      list.splice(idx, 1)
      this.delegationBindings.set(machineId, list)
      return true
    }
    return false
  }

  public resolveResponder(machineId: string, discipline: string, scheduleType: 'Shift' | 'OfficeTime' | 'OnCall' = 'Shift'): {
    recipeName: string
    discipline: string
    assigneeId?: string
    assigneeName?: string
    groupName?: string
    isOnCallFallback: boolean
    reason: string
  } {
    const bindings = (this.delegationBindings.get(machineId) || []).filter(b => b.isActive)
    const match = bindings.find(b => b.discipline.toLowerCase() === discipline.toLowerCase()) || bindings[0]

    if (!match) {
      return {
        recipeName: 'Default Station Preferred Technician',
        discipline,
        assigneeId: 'usr-orwell',
        assigneeName: 'Engineer Orwell',
        isOnCallFallback: false,
        reason: 'No delegation recipe bound to machine. Fallback to default dedicated lead engineer.'
      }
    }

    const recipe = this.delegationRecipes.get(match.delegationRecipeId)
    if (!recipe) {
      return {
        recipeName: match.delegationRecipeName,
        discipline,
        isOnCallFallback: false,
        reason: 'Bound recipe definition not found.'
      }
    }

    if (scheduleType === 'OnCall' && recipe.onCallFallbackUserId) {
      return {
        recipeName: recipe.name,
        discipline: recipe.discipline,
        assigneeId: recipe.onCallFallbackUserId,
        assigneeName: recipe.onCallFallbackUserName || recipe.onCallFallbackUserId,
        isOnCallFallback: true,
        reason: `Off-hours / on-call standby triggered. Routed to designated on-call fallback specialist.`
      }
    }

    if (recipe.requiredSkillCodes && recipe.requiredSkillCodes.length > 0) {
      // Find certified personnel
      const certified = Array.from(this.personnelSkills.values()).find(ps => 
        recipe.requiredSkillCodes.includes(ps.skillCode) &&
        (!ps.expiresAt || new Date(ps.expiresAt).getTime() > Date.now())
      )

      if (certified) {
        return {
          recipeName: recipe.name,
          discipline: recipe.discipline,
          assigneeId: certified.userId,
          assigneeName: certified.userName || certified.userId,
          groupName: recipe.primaryPersonnelGroupName,
          isOnCallFallback: false,
          reason: `Resolved to certified specialist holding valid ${certified.skillCode} (${certified.skillName}).`
        }
      }
    }

    return {
      recipeName: recipe.name,
      discipline: recipe.discipline,
      assigneeId: recipe.primaryUserId,
      assigneeName: recipe.primaryUserName,
      groupName: recipe.primaryPersonnelGroupName,
      isOnCallFallback: false,
      reason: `Resolved to primary shift crew "${recipe.primaryPersonnelGroupName || 'Shift Group'}" member.`
    }
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

  // --- Software Assets Quadrant ---
  public getSoftwareAssets(machineId: string): SoftwareAssetEntry[] {
    return this.softwareAssets.get(machineId) || []
  }

  public addSoftwareAsset(asset: Omit<SoftwareAssetEntry, 'id' | 'updatedAt'>): SoftwareAssetEntry {
    const entry: SoftwareAssetEntry = {
      id: `sw-${randomUUID().slice(0, 8)}`,
      ...asset,
      updatedAt: new Date().toISOString()
    }
    const list = this.softwareAssets.get(asset.machineId) || []
    list.push(entry)
    this.softwareAssets.set(asset.machineId, list)
    return entry
  }

  // --- Licenses Quadrant ---
  public getLicenses(machineId: string): LicenseAssetEntry[] {
    return this.licenseAssets.get(machineId) || []
  }

  // --- Documents Quadrant ---
  public getDocuments(machineId: string): MachineDocumentEntry[] {
    return this.documents.get(machineId) || []
  }

  // --- Operational Journal / Service Logs ---
  public getServiceLogs(machineId: string): MachineServiceLogEntry[] {
    return (this.serviceLogs.get(machineId) || []).sort(
      (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    )
  }

  public addServiceLog(log: Omit<MachineServiceLogEntry, 'id' | 'timestamp'>): MachineServiceLogEntry {
    const entry: MachineServiceLogEntry = {
      id: `log-${randomUUID().slice(0, 8)}`,
      timestamp: new Date().toISOString(),
      ...log
    }
    const list = this.serviceLogs.get(log.machineId) || []
    list.push(entry)
    this.serviceLogs.set(log.machineId, list)
    return entry
  }
}

export const componentsAndDelegationsStore = new ComponentsAndDelegationsStore()
