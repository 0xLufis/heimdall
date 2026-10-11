import type { TelemetryTemplate } from '~/types/telemetry'

export const BUILTIN_TELEMETRY_TEMPLATES: TelemetryTemplate[] = [
  {
    recipeId: 'tpl-motion-beckhoff-01',
    version: '1.2.0',
    name: 'High-Frequency Motion Controller (Beckhoff ADS & EtherCAT)',
    description: 'Ultra-low jitter motion telemetry capturing servo torques, EtherCAT CRC errors, and TwinCAT runtime states.',
    isBuiltin: true,
    targetSelector: {
      osPlatform: 'Windows',
      controllerRoles: ['MotionControl', 'RoboticsMaster', 'PLC_Runtime'],
      tags: { Line: 'Line-A', Class: 'Beckhoff-IPC' }
    },
    security: {
      keyId: 'pki-edge-sec-key-01',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: true
    },
    dataPoints: [
      {
        pointId: 'dp-tc-ads-state',
        name: 'TwinCAT ADS Run State',
        description: 'Verifies real-time kernel operational mode (Run/Stop/Exception)',
        sourceType: 'BeckhoffAds',
        dataCategory: 'DeviceState',
        egressPriority: 'P0_CriticalAlarm',
        schedule: { strategy: 'ChangeOfValue' },
        deadband: { deadbandType: 'StateChangeOnly' },
        connectionParameter: '127.0.0.1.1.1:851',
        pathOrSymbol: 'MAIN.fbSystemState.AdsState'
      },
      {
        pointId: 'dp-ecat-crc-errors',
        name: 'EtherCAT Bus CRC Fault Counter',
        description: 'Monitors frame drop and packet corruption on industrial fieldbus',
        sourceType: 'BeckhoffEtherCat',
        dataCategory: 'Metric',
        egressPriority: 'P1_HighOperational',
        schedule: { strategy: 'Periodic', intervalMs: 1000 },
        deadband: { deadbandType: 'Absolute', deadbandValue: 1 },
        connectionParameter: 'MasterDevice:0',
        pathOrSymbol: 'ESC.Port0.CrcErrors'
      },
      {
        pointId: 'dp-servo-torque-actual',
        name: 'Spindle Actual Torque & Current',
        description: 'High-speed torque feedback in Nm',
        sourceType: 'BeckhoffAds',
        dataCategory: 'Scalar',
        egressPriority: 'P2_MediumMetrics',
        schedule: { strategy: 'Periodic', intervalMs: 250 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 2.0 },
        connectionParameter: '127.0.0.1.1.1:851',
        pathOrSymbol: 'Axis1.ActTorque'
      }
    ]
  },
  {
    recipeId: 'tpl-ipc-baseline-02',
    version: '1.0.0',
    name: 'Standard Factory IPC Baseline (System + CIM)',
    description: 'Essential hardware telemetry, CPU/RAM/Disk metrics, storage wear leveling, and network egress counters.',
    isBuiltin: true,
    targetSelector: {
      osPlatform: 'All',
      controllerRoles: ['GeneralIPC', 'HMI_Terminal', 'EdgeNode'],
      tags: { Environment: 'ShopFloor' }
    },
    security: {
      keyId: 'pki-baseline-key',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: false
    },
    dataPoints: [
      {
        pointId: 'dp-cpu-load',
        name: 'Host CPU Utilization',
        description: 'Aggregated core utilization percentage',
        sourceType: 'SystemCim',
        dataCategory: 'Metric',
        egressPriority: 'P2_MediumMetrics',
        schedule: { strategy: 'Periodic', intervalMs: 5000 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 1.5 },
        pathOrSymbol: 'Win32_PerfFormattedData_PerfOS_Processor.PercentProcessorTime'
      },
      {
        pointId: 'dp-ram-load',
        name: 'Host Memory Consumption',
        description: 'Physical RAM active utilization percentage',
        sourceType: 'SystemCim',
        dataCategory: 'Metric',
        egressPriority: 'P2_MediumMetrics',
        schedule: { strategy: 'Periodic', intervalMs: 5000 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 2.0 },
        pathOrSymbol: 'Win32_OperatingSystem.FreePhysicalMemory'
      },
      {
        pointId: 'dp-disk-storage',
        name: 'Storage Drive Capacity & Free Space',
        description: 'Monitors C: and D: volume free percentages',
        sourceType: 'SystemDisk',
        dataCategory: 'Map',
        egressPriority: 'P3_LowInventory',
        schedule: { strategy: 'Periodic', intervalMs: 30000 },
        deadband: { deadbandType: 'Absolute', deadbandValue: 0.5 },
        pathOrSymbol: 'Drives.*.FreeSpaceGB'
      }
    ]
  },
  {
    recipeId: 'tpl-vision-cognex-03',
    version: '1.1.0',
    name: 'Vision Inspection Station (Cognex / Optical AI)',
    description: 'Vision inspection station monitoring optical inspection pass/fail counters, camera trigger rates, and defect counts.',
    isBuiltin: true,
    targetSelector: {
      osPlatform: 'Windows',
      controllerRoles: ['VisionInspector', 'QualityAudit'],
      tags: { Cell: 'Quality-Cell' }
    },
    security: {
      keyId: 'pki-vision-key',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: true
    },
    dataPoints: [
      {
        pointId: 'dp-cam-trigger-count',
        name: 'Camera Part Trigger Count',
        description: 'Total number of optical inspections fired',
        sourceType: 'TcpSocket',
        dataCategory: 'Scalar',
        egressPriority: 'P1_HighOperational',
        schedule: { strategy: 'ChangeOfValue' },
        deadband: { deadbandType: 'Absolute', deadbandValue: 1 },
        connectionParameter: '192.168.10.50:3000',
        pathOrSymbol: 'TriggerCount'
      },
      {
        pointId: 'dp-vision-fail-rate',
        name: 'Defect Detection Rate',
        description: 'Percentage of rejected assemblies over sliding 100-part window',
        sourceType: 'SystemProcess',
        dataCategory: 'Metric',
        egressPriority: 'P0_CriticalAlarm',
        schedule: { strategy: 'Periodic', intervalMs: 2000 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 0.5 },
        pathOrSymbol: 'VisionPro.exe.DefectRate'
      }
    ]
  },
  {
    recipeId: 'tpl-scada-opcua-04',
    version: '2.0.0',
    name: 'SCADA Gateway (OPC UA / Modbus TCP)',
    description: 'Telemetry bridge subscribing to PLC nodes over OPC UA and Modbus register blocks.',
    isBuiltin: true,
    targetSelector: {
      osPlatform: 'Linux',
      controllerRoles: ['ScadaGateway', 'ProtocolBridge'],
      tags: { Subnet: '10.10.20.0/24' }
    },
    security: {
      keyId: 'pki-scada-key',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: true
    },
    dataPoints: [
      {
        pointId: 'dp-opcua-station-status',
        name: 'Line Execution Status',
        description: 'OPC UA monitored item for machine operational state',
        sourceType: 'OpcUaSubscription',
        dataCategory: 'DeviceState',
        egressPriority: 'P0_CriticalAlarm',
        schedule: { strategy: 'ChangeOfValue' },
        deadband: { deadbandType: 'StateChangeOnly' },
        connectionParameter: 'opc.tcp://10.10.20.10:4840',
        pathOrSymbol: 'ns=2;s=Line1.Station.Status'
      },
      {
        pointId: 'dp-modbus-holding-registers',
        name: 'Energy Consumption & Line Voltage',
        description: 'Modbus 40001-40010 electrical telemetry block',
        sourceType: 'ModbusTcp',
        dataCategory: 'List',
        egressPriority: 'P2_MediumMetrics',
        schedule: { strategy: 'Periodic', intervalMs: 10000 },
        deadband: { deadbandType: 'Percentage', deadbandValue: 3.0 },
        connectionParameter: '10.10.20.15:502',
        pathOrSymbol: 'Register:40001:10'
      }
    ]
  },
  {
    recipeId: 'tpl-minimal-egress-05',
    version: '1.0.0',
    name: 'Ultra-Low Bandwidth Edge (xxHash64 Delta + Deadband)',
    description: 'Optimized for cellular, satellite, or constrained industrial wireless with strict xxHash64 delta hashing and 5% deadbands.',
    isBuiltin: true,
    targetSelector: {
      osPlatform: 'All',
      controllerRoles: ['RemoteNode', 'ConstrainedLink'],
      tags: { Egress: 'Metered' }
    },
    security: {
      keyId: 'pki-lowbw-key',
      algorithm: 'RSA_PSS_SHA256',
      signPayload: false
    },
    dataPoints: [
      {
        pointId: 'dp-heartbeat-state',
        name: 'Edge Node Heartbeat',
        description: 'Low-frequency delta heartbeat',
        sourceType: 'SystemCim',
        dataCategory: 'DeviceState',
        egressPriority: 'P3_LowInventory',
        schedule: { strategy: 'Periodic', intervalMs: 60000 },
        deadband: { deadbandType: 'StateChangeOnly' },
        pathOrSymbol: 'System.State'
      }
    ]
  }
]
