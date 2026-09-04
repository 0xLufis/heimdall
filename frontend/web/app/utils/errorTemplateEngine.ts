import type { ErrorTemplate, TicketStatus, FunctionBlockState } from '~/types/maintenance'

export const ERROR_TEMPLATES: ErrorTemplate[] = [
  // ── CATEGORY: ERROR ──────────────────────────────────────────────────────
  // Motion & Drive
  {
    id: 'E-MOT-01',
    category: 'Error',
    errorGroup: 'Motion & Drive',
    errorCode: 'E-MOT-01',
    shortDescription: 'Axis Position Divergence',
    detailedDescription: 'Servo axis commanded position diverges from actual encoder feedback beyond the permitted following error window. Check drive amplifier, encoder cable integrity, and mechanical coupling.',
    targetKanbanState: 'In_Progress',
    defaultTags: ['#Motion', '#Axis', '#Servo'],
    sampleFbState: { blockName: 'FB_AxisControl', state: 'ERROR_STOP', subState: 'FollowingError', errorCode: '16#4330' },
    sampleTelemetryKeys: ['following_error_mm', 'commanded_velocity', 'actual_velocity', 'motor_current_A'],
    affectedMachineTypes: ['Milling', 'Manipulator', 'Pressing', 'Fitting'],
  },
  {
    id: 'E-MOT-02',
    category: 'Error',
    errorGroup: 'Motion & Drive',
    errorCode: 'E-MOT-02',
    shortDescription: 'Servo Drive Over-Torque',
    detailedDescription: 'Drive torque limit exceeded continuously for >500 ms. Possible mechanical jam, worn tooling, or incorrect work clamping. Check part fixture and toolpath.',
    targetKanbanState: 'In_Progress',
    defaultTags: ['#Motion', '#Torque', '#Overload'],
    sampleFbState: { blockName: 'FB_DriveControl', state: 'FAULT', subState: 'OverTorque', errorCode: '16#7300' },
    sampleTelemetryKeys: ['torque_Nm', 'torque_limit_Nm', 'peak_current_A', 'ambient_temp_C'],
    affectedMachineTypes: ['Milling', 'Pressing', 'Screwing Station'],
  },
  // Safety
  {
    id: 'E-SAFE-01',
    category: 'Error',
    errorGroup: 'Safety System',
    errorCode: 'E-SAFE-01',
    shortDescription: 'Light Curtain Muting Fault',
    detailedDescription: 'Safety light curtain muting sequence failed or timed out. Dual-channel safety relay detected desynchronization >5 ms. Immediate escalation required — machine must remain halted until safety engineer signs off.',
    targetKanbanState: 'Escalated',
    defaultTags: ['#Safety', '#LightCurtain', '#SIL2'],
    sampleFbState: { blockName: 'FB_SafetyDoor', state: 'SAFE_STOP_2', subState: 'MutingTimeout', errorCode: '16#F001' },
    sampleTelemetryKeys: ['channel_A_ok', 'channel_B_ok', 'muting_lamp_ok', 'response_time_ms'],
    affectedMachineTypes: ['Pressing', 'Manipulator', 'Milling', 'Fitting', 'Screwing Station'],
  },
  {
    id: 'E-SAFE-02',
    category: 'Error',
    errorGroup: 'Safety System',
    errorCode: 'E-SAFE-02',
    shortDescription: 'E-Stop Dual Channel Desync',
    detailedDescription: 'Emergency stop button dual safety channels desynced beyond allowable 100 ms window. Safety PLC triggered category 0 stop. Do not reset without safety engineer verification.',
    targetKanbanState: 'Escalated',
    defaultTags: ['#Safety', '#EStop', '#DualChannel'],
    sampleFbState: { blockName: 'FB_EStop', state: 'FAULT', subState: 'ChannelDesync', errorCode: '16#F002' },
    sampleTelemetryKeys: ['ch_A_response_ms', 'ch_B_response_ms', 'delta_ms'],
    affectedMachineTypes: ['Pressing', 'Manipulator', 'Milling', 'Fitting', 'Screwing Station', 'Soldering', 'Gap Filler'],
  },
  // Fieldbus & Network
  {
    id: 'E-NET-01',
    category: 'Error',
    errorGroup: 'Fieldbus & Network',
    errorCode: 'E-NET-01',
    shortDescription: 'SAP MES RFC Sync Dropout',
    detailedDescription: 'Remote Function Call to SAP Production Order module returned timeout after 3 retries. Workpiece SFC confirmation pending. SAP engineers must verify RFC destination and service user locks.',
    targetKanbanState: 'Escalated_External',
    externalEscalationTarget: 'SAP Engineers',
    defaultTags: ['#MES', '#SAP', '#RFC', '#Network'],
    sampleFbState: { blockName: 'FB_MES_Interface', state: 'COMM_ERROR', subState: 'RFC_Timeout', errorCode: '16#8001' },
    sampleTelemetryKeys: ['rfc_latency_ms', 'retry_count', 'last_success_ts'],
    affectedMachineTypes: ['Automatic Optical Inspection', 'Tester Cell', 'Screwing Station'],
  },
  // Vision & Optical
  {
    id: 'E-VIS-01',
    category: 'Error',
    errorGroup: 'Vision & Optical',
    errorCode: 'E-VIS-01',
    shortDescription: 'AOI Blob Detection Reject Limit',
    detailedDescription: 'Cognex In-Sight camera blob detection reject rate exceeded 3% of parts over last 100 cycle window. Possible lens contamination, illumination degradation, or component placement drift.',
    targetKanbanState: 'Open',
    defaultTags: ['#AOI', '#Vision', '#Cognex', '#Reject'],
    sampleFbState: { blockName: 'FB_CameraControl', state: 'RUNNING', subState: 'RejectLimitExceeded', errorCode: '16#3001' },
    sampleTelemetryKeys: ['reject_rate_pct', 'good_parts', 'rejected_parts', 'last_defect_type'],
    affectedMachineTypes: ['Automatic Optical Inspection'],
  },
  // Dispensing
  {
    id: 'E-DISP-01',
    category: 'Error',
    errorGroup: 'Dispensing',
    errorCode: 'E-DISP-01',
    shortDescription: 'Gap Filler Nozzle Pressure Sag',
    detailedDescription: 'Dispensing nozzle outlet pressure dropped below lower control limit (3.2 bar). Possible nozzle clog, worn pump seal, or empty material cartridge. Replacement parts required.',
    targetKanbanState: 'Pending_Parts',
    defaultTags: ['#GapFiller', '#Dispensing', '#Pressure', '#Parts'],
    sampleFbState: { blockName: 'FB_DispensingControl', state: 'MATERIAL_LOW', subState: 'PressureSag', errorCode: '16#5001' },
    sampleTelemetryKeys: ['outlet_pressure_bar', 'setpoint_bar', 'flow_rate_ml_min', 'cartridge_weight_g'],
    affectedMachineTypes: ['Gap Filler'],
  },
  // Screwing
  {
    id: 'E-SCRW-01',
    category: 'Error',
    errorGroup: 'Screwing & Fastening',
    errorCode: 'E-SCRW-01',
    shortDescription: 'Final Torque Angle Window Exceeded',
    detailedDescription: 'Fastening cycle completed outside of the programmed angle window. Final torque achieved but seat angle is outside ±5° tolerance. Part requires rework inspection.',
    targetKanbanState: 'Open',
    defaultTags: ['#Screwing', '#Torque', '#Angle', '#NOK'],
    sampleFbState: { blockName: 'FB_NutRunner', state: 'NOK', subState: 'AngleWindowExceeded', errorCode: '16#6001' },
    sampleTelemetryKeys: ['final_torque_Nm', 'target_torque_Nm', 'seat_angle_deg', 'angle_window_deg'],
    affectedMachineTypes: ['Screwing Station'],
  },
  // Pressing
  {
    id: 'E-PRES-01',
    category: 'Error',
    errorGroup: 'Pressing & Joining',
    errorCode: 'E-PRES-01',
    shortDescription: 'Force-Displacement Envelope Violation',
    detailedDescription: 'Servo press force-displacement curve fell outside the programmed upper/lower tolerance envelope at position 12.4 mm. Part likely has dimension variance or incorrect material grade.',
    targetKanbanState: 'Open',
    defaultTags: ['#Pressing', '#ForceDisplacement', '#NOK'],
    sampleFbState: { blockName: 'FB_ServoPressControl', state: 'NOK', subState: 'EnvelopeViolation', errorCode: '16#7001' },
    sampleTelemetryKeys: ['peak_force_kN', 'displacement_at_peak_mm', 'envelope_upper_kN', 'envelope_lower_kN'],
    affectedMachineTypes: ['Pressing', 'Fitting'],
  },
  // Soldering
  {
    id: 'E-SOLD-01',
    category: 'Error',
    errorGroup: 'Soldering & Thermal',
    errorCode: 'E-SOLD-01',
    shortDescription: 'Induction Heater Thermal Lag',
    detailedDescription: 'Induction heater failed to reach target temperature of 280°C within 8 s ramp timeout. Possible coil gap mismatch, component heat-sink variance, or inverter output degradation.',
    targetKanbanState: 'In_Progress',
    defaultTags: ['#Soldering', '#Thermal', '#Induction'],
    sampleFbState: { blockName: 'FB_InductionHeater', state: 'FAULT', subState: 'RampTimeout', errorCode: '16#8002' },
    sampleTelemetryKeys: ['actual_temp_C', 'target_temp_C', 'ramp_time_s', 'inverter_power_kW'],
    affectedMachineTypes: ['Soldering'],
  },
  // Milling
  {
    id: 'E-MILL-01',
    category: 'Error',
    errorGroup: 'Milling & Machining',
    errorCode: 'E-MILL-01',
    shortDescription: 'Spindle Vibration Harmonic Peak',
    detailedDescription: 'Spindle vibration harmonic peak exceeded 4.5 mm/s RMS at 3,200 Hz frequency band. Likely toolholder imbalance or tool wear. Spindle balance check and tool replacement required.',
    targetKanbanState: 'In_Progress',
    defaultTags: ['#Milling', '#Vibration', '#Spindle', '#ToolWear'],
    sampleFbState: { blockName: 'FB_SpindleControl', state: 'WARNING', subState: 'VibrationHigh', errorCode: '16#9001' },
    sampleTelemetryKeys: ['vibration_rms_mm_s', 'spindle_rpm', 'dominant_freq_Hz', 'tool_life_pct'],
    affectedMachineTypes: ['Milling'],
  },

  // ── CATEGORY: PREVENTION ─────────────────────────────────────────────────
  {
    id: 'P-CAL-01',
    category: 'Prevention',
    errorGroup: 'Calibration & Measurement',
    errorCode: 'P-CAL-01',
    shortDescription: 'Load Cell & Force Transducer Calibration',
    detailedDescription: 'Scheduled load cell and force transducer calibration cycle. Reference standard weights applied, calibration certificate required. Needs outside AOK sign-off before return to production.',
    targetKanbanState: 'Closure_Pending',
    defaultTags: ['#Calibration', '#PM', '#ForceCell'],
    sampleTelemetryKeys: ['zero_offset_mV', 'span_error_pct', 'ref_weight_kg'],
    affectedMachineTypes: ['Pressing', 'Screwing Station', 'Fitting'],
  },
  {
    id: 'P-OPT-01',
    category: 'Prevention',
    errorGroup: 'Optics & Vision Maintenance',
    errorCode: 'P-OPT-01',
    shortDescription: 'Vision Lighting & Lens Cleaning',
    detailedDescription: 'Scheduled cleaning of strobe ring lights, telecentric lens front element, and diffuser panels on AOI station. Reject rate baseline recheck required after cleaning.',
    targetKanbanState: 'Open',
    defaultTags: ['#PM', '#AOI', '#Optics', '#Cleaning'],
    affectedMachineTypes: ['Automatic Optical Inspection'],
  },
  {
    id: 'P-LUB-01',
    category: 'Prevention',
    errorGroup: 'Lubrication & Wear',
    errorCode: 'P-LUB-01',
    shortDescription: 'Ball Screw Automatic Greasing Cycle',
    detailedDescription: 'Preventive lubrication of ball screw, linear guides, and Z-axis lead screw. Grease type: Klüber Isoflex NBU 15. Quantity per point: 2 g. Check wipers for wear after lubrication.',
    targetKanbanState: 'Open',
    defaultTags: ['#PM', '#Lubrication', '#BallScrew'],
    affectedMachineTypes: ['Milling', 'Manipulator', 'Pressing', 'Fitting'],
  },

  // ── CATEGORY: IMPROVEMENT ────────────────────────────────────────────────
  {
    id: 'I-CYC-01',
    category: 'Improvement',
    errorGroup: 'Cycle Time Optimization',
    errorCode: 'I-CYC-01',
    shortDescription: 'Robot Trajectory Cycle Reduction',
    detailedDescription: 'Opportunity to reduce robot pick-and-place cycle time by 800 ms via optimised blend radius and reduced intermediate waypoints. Requires offline simulation sign-off before deployment.',
    targetKanbanState: 'Open',
    defaultTags: ['#Improvement', '#CycleTime', '#Robot'],
    affectedMachineTypes: ['Manipulator'],
  },
  {
    id: 'I-REJ-01',
    category: 'Improvement',
    errorGroup: 'Quality & Vision AI',
    errorCode: 'I-REJ-01',
    shortDescription: 'Solder Joint Classifier Retraining',
    detailedDescription: 'AOI solder joint defect classifier false positive rate increased to 1.8% after component reel change. Retraining with 500 new labelled images recommended. Model update requires IT and quality sign-off.',
    targetKanbanState: 'Open',
    defaultTags: ['#Improvement', '#AI', '#AOI', '#Quality'],
    affectedMachineTypes: ['Automatic Optical Inspection', 'Soldering'],
  },

  // ── CATEGORY: ETC ────────────────────────────────────────────────────────
  {
    id: 'ETC-FW-01',
    category: 'ETC',
    errorGroup: 'Firmware & Software',
    errorCode: 'ETC-FW-01',
    shortDescription: 'TwinCAT PLC Runtime Firmware Rollup',
    detailedDescription: 'Scheduled TwinCAT 3 runtime firmware update rollup. Version target: 4024.56 → 4026.10. Requires IT change request, controlled restart window, and verification of all function blocks post-upgrade.',
    targetKanbanState: 'Open',
    defaultTags: ['#Firmware', '#TwinCAT', '#PLC', '#IT'],
    affectedMachineTypes: ['Milling', 'Pressing', 'Screwing Station', 'Manipulator', 'Gap Filler'],
  },
]

export const CATEGORIES = ['Prevention', 'Error', 'Improvement', 'ETC'] as const

export const ERROR_GROUPS = [...new Set(ERROR_TEMPLATES.map(t => t.errorGroup))]

export function getTemplatesByCategory(category: string): ErrorTemplate[] {
  return ERROR_TEMPLATES.filter(t => t.category === category)
}

export function getTemplatesByGroup(group: string): ErrorTemplate[] {
  return ERROR_TEMPLATES.filter(t => t.errorGroup === group)
}

export function getTemplateById(id: string): ErrorTemplate | undefined {
  return ERROR_TEMPLATES.find(t => t.id === id)
}

export function getGroupsByCategory(category: string): string[] {
  return [...new Set(ERROR_TEMPLATES.filter(t => t.category === category).map(t => t.errorGroup))]
}

export function applyTemplate(template: ErrorTemplate, context: {
  stationName?: string
  sfcSerial?: string
  technicianName?: string
}): { title: string; description: string; tags: string[] } {
  const sfcPart = context.sfcSerial ? ` — SFC: ${context.sfcSerial}` : ''
  const stationPart = context.stationName ? ` @ ${context.stationName}` : ''
  return {
    title: `[${template.errorCode}] ${template.shortDescription}${stationPart}${sfcPart}`,
    description: template.detailedDescription,
    tags: [
      ...template.defaultTags,
      ...(context.sfcSerial ? [`#SFC-${context.sfcSerial.split('-').pop()}`] : []),
    ],
  }
}

export const MACHINE_TYPES = [
  'Automatic Optical Inspection',
  'Gap Filler',
  'Screwing Station',
  'Soldering',
  'Milling',
  'Fitting',
  'Pressing',
  'Manipulator',
  'Tester Cell',
  'Painting',
] as const

export type MachineTypeName = typeof MACHINE_TYPES[number]
