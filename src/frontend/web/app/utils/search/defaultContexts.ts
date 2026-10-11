import type { SearchContext, SearchSource } from './searchContext'

export const SAMPLE_MACHINES_DATA = [
  { id: 'mch-1', name: 'KUKA KR-210 R2700', manufacturer: 'KUKA', status: 'online', location: 'Building A', tech: 'Robotics', line: 'Line 1' },
  { id: 'mch-2', name: 'Siemens S7-1500 Cell PLC', manufacturer: 'Siemens', status: 'online', location: 'Building A', tech: 'PLC', line: 'Line 1' },
  { id: 'mch-3', name: 'Beckhoff CX2040 IPC', manufacturer: 'Beckhoff', status: 'warning', location: 'Building B', tech: 'IPC', line: 'Line 2' },
  { id: 'mch-4', name: 'Cognex In-Sight 7800 Camera', manufacturer: 'Cognex', status: 'online', location: 'Building B', tech: 'Vision', line: 'Line 2' },
  { id: 'mch-5', name: 'Advantech MIC-770 Edge Host', manufacturer: 'Advantech', status: 'offline', location: 'Building C', tech: 'IPC', line: 'Line 3' }
]

export const SAMPLE_CLIENT_PCS_DATA = [
  { id: 'ipc-1', hostname: 'IPC-WELD-01', ipAddress: '192.168.10.41', osVersion: 'Windows 11 IoT Enterprise', status: 'online', location: 'Line 1', ouPath: 'OU=Robotics,DC=plant,DC=internal' },
  { id: 'ipc-2', hostname: 'IPC-CNC-02', ipAddress: '192.168.10.42', osVersion: 'Windows 10 IoT LTSC', status: 'online', location: 'Line 2', ouPath: 'OU=Machining,DC=plant,DC=internal' },
  { id: 'ipc-3', hostname: 'IPC-SMT-03', ipAddress: '192.168.10.43', osVersion: 'Ubuntu 22.04 LTS', status: 'warning', location: 'Line 3', ouPath: 'OU=Electronics,DC=plant,DC=internal' }
]

export const SAMPLE_TICKETS_DATA = [
  { id: 'tkt-1', ticketNumber: 'TKT-2026-001', title: 'Axis 4 Over-Temperature Shutdown', status: 'Open', priority: 'Critical', department: 'Robotics', stationName: 'KUKA KR-210' },
  { id: 'tkt-2', ticketNumber: 'TKT-2026-002', title: 'EtherCAT Communication Timeout', status: 'InProgress', priority: 'High', department: 'Controls', stationName: 'Beckhoff CX2040' },
  { id: 'tkt-3', ticketNumber: 'TKT-2026-003', title: 'Periodic Vision Lens Cleaning', status: 'Resolved', priority: 'Low', department: 'Quality', stationName: 'Cognex In-Sight' }
]

export const SAMPLE_INVENTORY_DATA = [
  { id: 'inv-1', name: 'Siemens 1FK7060 Servomotor', category: 'Motor', manufacturer: 'Siemens', location: 'Shelf A-12', status: 'InStorage', serialNumber: 'SRV-88412' },
  { id: 'inv-2', name: 'Beckhoff EL1008 Digital Input', category: 'PLC', manufacturer: 'Beckhoff', location: 'Cabinet C-04', status: 'InStorage', serialNumber: 'EL-00492' },
  { id: 'inv-3', name: 'Festo DNC-63 Pneumatic Cylinder', category: 'Valve', manufacturer: 'Festo', location: 'Bin 14', status: 'InMachine', serialNumber: 'CYL-9931' }
]

export const SAMPLE_USERS_DATA = [
  { id: 'usr-1', name: 'Gábor Varga', email: 'varga.gabor@heimdall.plant', role: 'maintenance_lead', department: 'Maintenance' },
  { id: 'usr-2', name: 'Elena Rostova', email: 'elena.rostova@heimdall.plant', role: 'it_admin', department: 'IT Operations' },
  { id: 'usr-3', name: 'Markus Lindqvist', email: 'm.lindqvist@heimdall.plant', role: 'controls_engineer', department: 'Controls' }
]

export const defaultMachinesSource: SearchSource = {
  id: 'machines',
  name: 'Production Machines',
  entityType: 'Machine',
  indicatorLabel: 'MCH',
  indicatorColor: 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/20',
  data: SAMPLE_MACHINES_DATA,
  indexedKey: 'id',
  columns: ['name', 'manufacturer', 'status', 'location', 'tech', 'line'],
  linkGenerator: (item) => `/dashboard/machines?id=${item.id}`
}

export const defaultClientPcsSource: SearchSource = {
  id: 'client_pcs',
  name: 'Client IPC Controllers',
  entityType: 'ClientPc',
  indicatorLabel: 'IPC',
  indicatorColor: 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-500/20',
  data: SAMPLE_CLIENT_PCS_DATA,
  indexedKey: 'id',
  columns: ['hostname', 'ipAddress', 'osVersion', 'status', 'location'],
  titleField: 'hostname',
  subtitleField: 'ipAddress',
  linkGenerator: (item) => `/dashboard/clients?id=${item.id}`
}

export const defaultTicketsSource: SearchSource = {
  id: 'tickets',
  name: 'Maintenance Tickets',
  entityType: 'Ticket',
  indicatorLabel: 'TKT',
  indicatorColor: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
  data: SAMPLE_TICKETS_DATA,
  indexedKey: 'id',
  columns: ['ticketNumber', 'title', 'status', 'priority', 'department'],
  titleField: 'title',
  subtitleField: 'ticketNumber',
  linkGenerator: (item) => `/dashboard/tickets?ticketId=${item.id}`
}

export const defaultInventorySource: SearchSource = {
  id: 'inventory',
  name: 'Parts & Inventory',
  entityType: 'InventoryItem',
  indicatorLabel: 'PART',
  indicatorColor: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
  data: SAMPLE_INVENTORY_DATA,
  indexedKey: 'id',
  columns: ['name', 'category', 'manufacturer', 'location', 'status', 'serialNumber'],
  linkGenerator: (item) => `/dashboard/inventory/${item.id}`
}

export const defaultUsersSource: SearchSource = {
  id: 'users',
  name: 'Staff & Personnel',
  entityType: 'User',
  indicatorLabel: 'USR',
  indicatorColor: 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20',
  data: SAMPLE_USERS_DATA,
  indexedKey: 'id',
  columns: ['name', 'email', 'role', 'department'],
  linkGenerator: (item) => `/dashboard/users/${item.id}`
}

export const defaultTemplatesSource: SearchSource = {
  id: 'templates',
  name: 'Asset Blueprints & Templates',
  entityType: 'AssetTemplate',
  indicatorLabel: 'TMPL',
  indicatorColor: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20',
  data: [
    { id: 'tmpl-1', name: 'Industrial IPC Standard', manufacturer: 'Siemens', topLevelCategory: 'Hardware', identifierPattern: 'IPC-{number}', tag: 'ipc' },
    { id: 'tmpl-2', name: 'Servo Drive Pack', manufacturer: 'Beckhoff', topLevelCategory: 'Hardware', identifierPattern: 'SRV-{number}', tag: 'motor' },
    { id: 'tmpl-3', name: 'Cognex Vision Sensor Blueprint', manufacturer: 'Cognex', topLevelCategory: 'Hardware', identifierPattern: 'VIS-{number}', tag: 'camera' }
  ],
  indexedKey: 'id',
  columns: ['name', 'manufacturer', 'topLevelCategory', 'identifierPattern', 'tag'],
  linkGenerator: (item) => `/dashboard/inventory?section=templates&templateId=${item.id}`
}

export const GLOBAL_SEARCH_CONTEXT: SearchContext = {
  id: 'global-plant',
  title: 'Global Plant Search',
  sources: [
    defaultMachinesSource,
    defaultClientPcsSource,
    defaultTicketsSource,
    defaultInventorySource,
    defaultTemplatesSource,
    defaultUsersSource
  ]
}

export const TICKETING_SEARCH_CONTEXT: SearchContext = {
  id: 'ticketing-workspace',
  title: 'Maintenance Incidents & Work Orders',
  sources: [
    defaultTicketsSource,
    defaultMachinesSource,
    defaultUsersSource
  ]
}

export const INVENTORY_SEARCH_CONTEXT: SearchContext = {
  id: 'inventory-workspace',
  title: 'Warehouse & Asset Inventory',
  sources: [
    defaultInventorySource,
    defaultTemplatesSource,
    defaultMachinesSource
  ]
}

export const defaultTelemetrySource: SearchSource = {
  id: 'telemetry',
  name: 'Telemetry Metrics & Data Points',
  entityType: 'TelemetryMetric',
  indicatorLabel: 'TEL',
  indicatorColor: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
  data: [
    { id: 'spindleSpeedRpm', name: 'Spindle Rotational Speed', category: 'Machining', dataType: 'Float64', unit: 'RPM', description: 'Actual speed of primary motor spindle' },
    { id: 'vibrationEnvelopeMmS', name: 'Harmonic Vibration RMS', category: 'Vibration', dataType: 'Float64', unit: 'mm/s', description: 'Tri-axial accelerometer vibration velocity' },
    { id: 'spindleTemperatureC', name: 'Spindle Bearing Temp', category: 'Thermal', dataType: 'Float64', unit: '°C', description: 'Front bearing PT100 temperature' },
    { id: 'cycleTimeMs', name: 'Station Tact Cycle Time', category: 'Cycle', dataType: 'Int32', unit: 'ms', description: 'TwinCAT PLC actual cycle duration' },
    { id: 'hydraulicPressureBar', name: 'System Hydraulic Pressure', category: 'Pneumatics', dataType: 'Float64', unit: 'bar', description: 'Main manifold supply pressure' },
    { id: 'plcState', name: 'TwinCAT Run State', category: 'PLC', dataType: 'String', unit: 'state', description: 'Beckhoff PLC execution state' },
    { id: 'cpuUsagePct', name: 'IPC Host CPU Utilization', category: 'Resources', dataType: 'Float64', unit: '%', description: 'Aggregated core processing workload across all physical cores' },
    { id: 'ramUsagePct', name: 'IPC System Memory Load', category: 'Resources', dataType: 'Float64', unit: '%', description: 'Physical RAM active allocation percentage' },
    { id: 'cycleJitterUs', name: 'Real-Time Soft-PLC Task Jitter', category: 'Jitter', dataType: 'Float64', unit: 'μs', description: 'Cyclic task scheduling jitter measured across TwinCAT real-time kernel cores' },
    { id: 'dcBusVoltageV', name: 'Inverter DC Bus Voltage', category: 'Electrical', dataType: 'Float64', unit: 'V', description: 'Intermediate circuit DC link voltage monitored across servo drives' },
    { id: 'fieldbusErrorRate', name: 'Fieldbus Frame Loss & CRC Errors', category: 'Fieldbus', dataType: 'Float64', unit: 'err/min', description: 'EtherCAT or PROFINET frame corruption and cyclic packet drop frequency' }
  ],
  indexedKey: 'id',
  columns: ['name', 'category', 'dataType', 'description', 'unit'],
  linkGenerator: (item) => `/dashboard/telemetry?tab=live&metric=${item.id}`
}

export const TELEMETRY_SEARCH_CONTEXT: SearchContext = {
  id: 'telemetry-workspace',
  title: 'Industrial Telemetry Hub & Live Streams',
  sources: [
    defaultTelemetrySource
  ]
}
