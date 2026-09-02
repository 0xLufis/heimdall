import type { AssetTemplate } from './jsonTemplatingEngine'

export const DEFAULT_ASSET_TEMPLATES: AssetTemplate[] = [
  {
    id: 'siemens-s7-1500',
    name: 'Siemens S7-1500 Modular PLC',
    category: 'Controller',
    icon: 'Cpu',
    description: 'High-performance Siemens SIMATIC S7-1500 PLC controller node with PROFINET interfaces, cyclic telemetry, and safety task capabilities.',
    targetType: 'HardwareComponent',
    tags: ['Siemens', 'PLC', 'PROFINET', 'OPC UA', 'Automation'],
    variables: [
      {
        name: 'stationCode',
        label: 'Production Station Code',
        type: 'string',
        defaultValue: 'OP10',
        placeholder: 'e.g. OP10, CELL-A, LINE-1',
        description: 'Target production station identifier',
        required: true
      },
      {
        name: 'cpuModel',
        label: 'CPU Model',
        type: 'select',
        defaultValue: 'CPU 1516-3 PN/DP',
        options: [
          'CPU 1511-1 PN',
          'CPU 1513-1 PN',
          'CPU 1515-2 PN',
          'CPU 1516-3 PN/DP',
          'CPU 1517-3 PN/DP',
          'CPU 1518-4 PN/DP MFP'
        ],
        description: 'Siemens S7-1500 central processing unit variant'
      },
      {
        name: 'ipAddress',
        label: 'Profinet IP Address',
        type: 'string',
        defaultValue: '192.168.10.10',
        placeholder: '192.168.x.x',
        description: 'Primary industrial Ethernet IP address',
        required: true
      },
      {
        name: 'firmwareVersion',
        label: 'Firmware Revision',
        type: 'string',
        defaultValue: 'V3.0.3',
        description: 'TIA Portal CPU firmware version'
      },
      {
        name: 'workMemoryKB',
        label: 'Work Memory (KB)',
        type: 'number',
        defaultValue: 1500,
        description: 'Code and data work memory capacity in KB'
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 1450000,
        description: 'Procurement unit cost in Hungarian Forints'
      }
    ],
    template: {
      name: 'PLC-{{stationCode | uppercase}}-01',
      displayName: 'Siemens S7-1500 Controller ({{stationCode | uppercase}})',
      technology: 'Siemens SIMATIC / PROFINET',
      serialNumber: 'SN-S7-{{$randomHex}}',
      modelNumber: '6ES7516-3AN02-0AB0',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        CPUModel: '{{cpuModel}}',
        IPAddress: '{{ipAddress}}',
        Firmware: '{{firmwareVersion}}',
        WorkMemory: '{{workMemoryKB}} KB',
        Protocol: 'PROFINET / OPC UA',
        CycleTimeTarget: '10ms',
        RackSlot: 'Rack 0 / Slot 1',
        Station: '{{stationCode | uppercase}}',
        OPCUAServerEnabled: true,
        LastCommissioned: '{{$date}}'
      }
    }
  },
  {
    id: 'beckhoff-cx5140',
    name: 'Beckhoff CX5140 TwinCAT 3 IPC',
    category: 'Controller',
    icon: 'Monitor',
    description: 'DIN-rail mounted Beckhoff Embedded Industrial PC with Intel Atom 4-core processor, TwinCAT 3 real-time kernel, and EtherCAT master.',
    targetType: 'HardwareComponent',
    tags: ['Beckhoff', 'IPC', 'TwinCAT 3', 'EtherCAT', 'Real-Time'],
    variables: [
      {
        name: 'stationCode',
        label: 'Station Code',
        type: 'string',
        defaultValue: 'OP20',
        placeholder: 'e.g. OP20, LINE-2',
        required: true
      },
      {
        name: 'amsNetId',
        label: 'TwinCAT AMS NetID',
        type: 'string',
        defaultValue: '192.168.20.10.1.1',
        description: 'Beckhoff AMS NetID for ADS communication'
      },
      {
        name: 'osVersion',
        label: 'Operating System',
        type: 'select',
        defaultValue: 'Windows 10 IoT Enterprise LTSC 64-bit',
        options: [
          'Windows 10 IoT Enterprise LTSC 64-bit',
          'Windows 11 IoT Enterprise',
          'TwinCAT/BSD (FreeBSD UNIX)'
        ]
      },
      {
        name: 'ramCapacity',
        label: 'System RAM',
        type: 'select',
        defaultValue: '8GB DDR3L',
        options: ['4GB DDR3L', '8GB DDR3L', '16GB DDR4']
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 1180000
      }
    ],
    template: {
      name: 'IPC-CX-{{stationCode | uppercase}}-01',
      displayName: 'Beckhoff CX5140 Real-Time IPC ({{stationCode | uppercase}})',
      technology: 'Beckhoff TwinCAT 3 / EtherCAT',
      serialNumber: 'SN-CX5140-{{$randomHex}}',
      modelNumber: 'CX5140-0175',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        Processor: 'Intel Atom E3845 (4 Cores, 1.91 GHz)',
        AMSNetID: '{{amsNetId}}',
        TwinCATVersion: 'v3.1.4024.55',
        OS: '{{osVersion}}',
        RAM: '{{ramCapacity}}',
        Storage: '64GB CFast MLC',
        Fieldbus: 'EtherCAT Master E-Bus',
        RealTimeTick: '1000us (1ms)',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'advantech-mic-770',
    name: 'Advantech MIC-770 V2 Industrial Workstation',
    category: 'Controller',
    icon: 'HardDrive',
    description: 'Rugged fanless modular industrial PC workstation with Intel Core i7 10th Gen, dual GPU support for AI edge inference, and multi-GbE LAN.',
    targetType: 'HardwareComponent',
    tags: ['Advantech', 'IPC', 'Edge AI', 'Industrial PC', 'Fanless'],
    variables: [
      {
        name: 'stationCode',
        label: 'Cell / Station Code',
        type: 'string',
        defaultValue: 'INSPECT-01',
        required: true
      },
      {
        name: 'cpuVariant',
        label: 'Processor',
        type: 'select',
        defaultValue: 'Intel Core i7-10700TE (8C/16T)',
        options: [
          'Intel Core i5-10500TE (6C/12T)',
          'Intel Core i7-10700TE (8C/16T)',
          'Intel Core i9-10900TE (10C/20T)',
          'Intel Xeon W-1290TE (10C/20T)'
        ]
      },
      {
        name: 'gpuModel',
        label: 'AI Acceleration GPU',
        type: 'select',
        defaultValue: 'NVIDIA RTX A2000 12GB',
        options: [
          'None (Integrated UHD Graphics)',
          'NVIDIA RTX A2000 12GB',
          'NVIDIA RTX 4060 Ti 16GB',
          'Intel Arc A380E'
        ]
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 1680000
      }
    ],
    template: {
      name: 'IPC-ADV-{{stationCode | uppercase}}',
      displayName: 'Advantech Edge Workstation ({{stationCode | uppercase}})',
      technology: 'Industrial Edge Computing / AI Vision',
      serialNumber: 'SN-MIC770-{{$randomHex}}',
      modelNumber: 'MIC-770V2-00A1',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        CPU: '{{cpuVariant}}',
        GPU: '{{gpuModel}}',
        RAM: '32GB DDR4 Industrial ECC',
        Storage: '1TB NVMe M.2 SSD PCIe Gen3',
        EthernetPorts: '4x Intel I210-IT GbE LAN',
        PowerInput: '9-36V DC Redundant',
        FormFactor: 'Fanless Rugged Aluminum Chassis',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'cognex-insight-9000',
    name: 'Cognex In-Sight 9000 Vision System',
    category: 'Vision',
    icon: 'Eye',
    description: 'Ultra-high-resolution 12-megapixel self-contained smart camera for micron-level part inspection, OCR reading, and robot guidance.',
    targetType: 'HardwareComponent',
    tags: ['Cognex', 'Vision', 'Smart Camera', 'GigE', 'Quality'],
    variables: [
      {
        name: 'stationCode',
        label: 'Station ID',
        type: 'string',
        defaultValue: 'OP30',
        required: true
      },
      {
        name: 'resolution',
        label: 'Sensor Resolution',
        type: 'select',
        defaultValue: '12 Megapixel (4096 x 3000)',
        options: [
          '5 Megapixel (2448 x 2048)',
          '12 Megapixel (4096 x 3000)',
          '16 Megapixel (4096 x 4096)'
        ]
      },
      {
        name: 'lensFocalLength',
        label: 'C-Mount Lens (mm)',
        type: 'select',
        defaultValue: '25mm F/1.4 High-Res',
        options: ['16mm F/1.4', '25mm F/1.4 High-Res', '35mm F/1.8 Telecentric', '50mm F/2.0 Precision']
      },
      {
        name: 'lightingType',
        label: 'Integrated Illumination',
        type: 'select',
        defaultValue: 'Coaxial Polarized White LED Array',
        options: [
          'Direct Ring Light (Red 630nm)',
          'Coaxial Polarized White LED Array',
          'Diffuse Dome Illumination',
          'UV Fluorescent 365nm Bar'
        ]
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 2350000
      }
    ],
    template: {
      name: 'CAM-COG-{{stationCode | uppercase}}-01',
      displayName: 'Cognex In-Sight 9000 Camera ({{stationCode | uppercase}})',
      technology: 'Cognex In-Sight Vision / GigE',
      serialNumber: 'SN-IS9912-{{$randomHex}}',
      modelNumber: 'IS9912M-300-00',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        Resolution: '{{resolution}}',
        FPS: '60 FPS Max Burst',
        SensorType: 'Monochrome CMOS Global Shutter',
        Lens: '{{lensFocalLength}}',
        Lighting: '{{lightingType}}',
        Interface: 'Gigabit Ethernet M12 X-Coded',
        IPRating: 'IP67 Waterproof Enclosure',
        Station: '{{stationCode | uppercase}}',
        InspectionTask: 'Precision Dimensional Metrology & Defect Verification'
      }
    }
  },
  {
    id: 'kuka-kr-c5',
    name: 'KUKA KR C5 Cybertech Robot Controller',
    category: 'Motion',
    icon: 'Bot',
    description: 'Compact 6-axis industrial articulated robot controller for high-speed material handling, gluing, and automated machine tending.',
    targetType: 'HardwareComponent',
    tags: ['KUKA', 'Robotics', 'Motion', 'CIP Safety', 'KR C5'],
    variables: [
      {
        name: 'stationCode',
        label: 'Robotic Cell / Station',
        type: 'string',
        defaultValue: 'CELL-01',
        required: true
      },
      {
        name: 'payloadKg',
        label: 'Payload Rating (kg)',
        type: 'number',
        defaultValue: 16,
        description: 'Maximum end-of-arm tooling payload in kg'
      },
      {
        name: 'reachMm',
        label: 'Working Reach (mm)',
        type: 'number',
        defaultValue: 1610,
        description: 'Spherical envelope reach radius in mm'
      },
      {
        name: 'kssVersion',
        label: 'KUKA System Software (KSS)',
        type: 'string',
        defaultValue: 'KSS 8.7.4'
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 8900000
      }
    ],
    template: {
      name: 'ROB-KUKA-{{stationCode | uppercase}}-01',
      displayName: 'KUKA Cybertech KR16 ({{stationCode | uppercase}})',
      technology: 'KUKA Robotics / SafeOperation',
      serialNumber: 'SN-KUKA-{{$randomHex}}',
      modelNumber: 'KR16 R1610 / KR C5 micro',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        Payload: '{{payloadKg}} kg',
        Reach: '{{reachMm}} mm',
        Axes: '6 Revolute Degrees of Freedom',
        Repeatability: '+/- 0.04 mm (ISO 9283)',
        KSSVersion: '{{kssVersion}}',
        SafetyInterface: 'CIP Safety over EtherNet/IP & PROFIsafe',
        TeachPendant: 'KUKA smartPAD-2 Touch',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'sinamics-s120',
    name: 'Siemens SINAMICS S120 Multi-Axis Servo Drive',
    category: 'Motion',
    icon: 'Zap',
    description: 'High-dynamics multi-axis modular servo drive inverter with DRIVE-CLiQ digital encoder feedback and integrated STO/SS1 safety.',
    targetType: 'HardwareComponent',
    tags: ['Siemens', 'SINAMICS', 'Servo Drive', 'Motion', 'Safety'],
    variables: [
      {
        name: 'stationCode',
        label: 'Station ID',
        type: 'string',
        defaultValue: 'OP20',
        required: true
      },
      {
        name: 'ratedCurrentA',
        label: 'Rated Output Current (A)',
        type: 'number',
        defaultValue: 30,
        description: 'Continuous sinusoidal output current'
      },
      {
        name: 'motorType',
        label: 'Coupled Servomotor',
        type: 'string',
        defaultValue: 'SIMOTICS S-1FK7 Synchronous Motor 6.8Nm'
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 1120000
      }
    ],
    template: {
      name: 'SRV-DRV-{{stationCode | uppercase}}-01',
      displayName: 'Siemens SINAMICS S120 Motor Drive ({{stationCode | uppercase}})',
      technology: 'Siemens Motion / DRIVE-CLiQ',
      serialNumber: 'SN-S120-{{$randomHex}}',
      modelNumber: '6SL3120-1TE23-0AA3',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        RatedCurrent: '{{ratedCurrentA}} A RMS',
        DCBusVoltage: '600V DC (510 - 720V)',
        EncoderInterface: 'DRIVE-CLiQ Digital Feedback',
        MotorCoupling: '{{motorType}}',
        IntegratedSafety: 'Safety Integrated STO / SS1 SIL3 PL e',
        ControlUnit: 'CU320-2 PN Vector/Servo',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'ifm-iolink-pressure',
    name: 'IFM Electronic IO-Link Pressure & Flow Sensor',
    category: 'Sensor',
    icon: 'Activity',
    description: 'Digital piezoresistive pressure transmitter and hydraulic flow meter with IO-Link v1.1 communication and process telemetry.',
    targetType: 'HardwareComponent',
    tags: ['IFM', 'IO-Link', 'Pressure Sensor', 'Flow', 'Instrumentation'],
    variables: [
      {
        name: 'stationCode',
        label: 'Station Identifier',
        type: 'string',
        defaultValue: 'OP10',
        required: true
      },
      {
        name: 'pressureRange',
        label: 'Pressure Range',
        type: 'select',
        defaultValue: '0 - 10 Bar (0 - 145 PSI)',
        options: [
          '0 - 6 Bar (0 - 87 PSI)',
          '0 - 10 Bar (0 - 145 PSI)',
          '0 - 25 Bar (0 - 362 PSI)',
          '0 - 100 Bar (0 - 1450 PSI)'
        ]
      },
      {
        name: 'portIndex',
        label: 'IO-Link Master Port',
        type: 'string',
        defaultValue: 'Port 1 / Channel A'
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 280000
      }
    ],
    template: {
      name: 'SNS-PRS-{{stationCode | uppercase}}-01',
      displayName: 'IFM Electronic Digital Pressure Transducer ({{stationCode | uppercase}})',
      technology: 'IFM IO-Link Process Instrumentation',
      serialNumber: 'SN-IFM-{{$randomHex}}',
      modelNumber: 'PN7094',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        PressureRange: '{{pressureRange}}',
        Protocol: 'IO-Link v1.1 COM2 (38.4 kBaud)',
        CycleTime: '2.3 ms Transmission Rate',
        ProcessConnection: 'G 1/4 A BSPP Threaded',
        MasterPort: '{{portIndex}}',
        OutputSignal: '4..20 mA Analog & IO-Link Switch Point',
        Medium: 'Hydraulic Oil / Coolant Emulsion / Compressed Air',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'beckhoff-ek1100',
    name: 'Beckhoff EK1100 EtherCAT Bus Coupler',
    category: 'Network',
    icon: 'Layers',
    description: 'High-speed fieldbus coupler connecting EtherCAT protocol to modular Beckhoff EL/ES series terminal slices with E-bus power feed.',
    targetType: 'HardwareComponent',
    tags: ['Beckhoff', 'EtherCAT', 'Bus Coupler', 'Remote IO', 'Fieldbus'],
    variables: [
      {
        name: 'stationCode',
        label: 'Station ID',
        type: 'string',
        defaultValue: 'OP10',
        required: true
      },
      {
        name: 'terminalCount',
        label: 'Attached I/O Terminals',
        type: 'number',
        defaultValue: 16,
        description: 'Number of discrete and analog slices connected'
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 145000
      }
    ],
    template: {
      name: 'NET-ECAT-{{stationCode | uppercase}}-01',
      displayName: 'Beckhoff EK1100 EtherCAT Coupler ({{stationCode | uppercase}})',
      technology: 'EtherCAT E-Bus Coupler',
      serialNumber: 'SN-EK1100-{{$randomHex}}',
      modelNumber: 'EK1100-0000',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        BusProtocol: 'EtherCAT 100BASE-TX (100 Mbit/s Full Duplex)',
        EBusCurrent: '2000 mA Power Supply Output',
        AttachedSlices: '{{terminalCount}} Terminals',
        TopologyPortIn: '1x RJ45 MII In',
        TopologyPortOut: '1x RJ45 MII Out (Daisy-Chain)',
        IsolationVoltage: '500 V RMS',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'nordson-ultimus-v',
    name: 'Nordson Ultimus V Precision Fluid Dispenser',
    category: 'Dispensing',
    icon: 'Pipette',
    description: 'Microprocessor-controlled fluid dispenser for thermal grease, anaerobic adhesive, or UV-curing conformal coatings with vacuum snuff-back.',
    targetType: 'HardwareComponent',
    tags: ['Nordson', 'Dispensing', 'Fluidics', 'Adhesive', 'Coating'],
    variables: [
      {
        name: 'stationCode',
        label: 'Dispense Cell / Station',
        type: 'string',
        defaultValue: 'DISP-01',
        required: true
      },
      {
        name: 'fluidType',
        label: 'Fluid Chemistry',
        type: 'select',
        defaultValue: 'Thermal Interface Paste (Gap Filler)',
        options: [
          'Thermal Interface Paste (Gap Filler)',
          'Loctite UV-Curing Acrylic 3108',
          'Epoxy 2-Component Potting Resin',
          'Silicone RTV Gasket Sealant'
        ]
      },
      {
        name: 'pressureLimitBar',
        label: 'Dispense Pressure (Bar)',
        type: 'number',
        defaultValue: 7.0
      },
      {
        name: 'costInHUF',
        label: 'Capital Cost (HUF)',
        type: 'number',
        defaultValue: 1890000
      }
    ],
    template: {
      name: 'DSP-NORD-{{stationCode | uppercase}}-01',
      displayName: 'Nordson Ultimus V Fluid Dispenser ({{stationCode | uppercase}})',
      technology: 'High-Precision Automated Fluid Dispensing',
      serialNumber: 'SN-ULTV-{{$randomHex}}',
      modelNumber: '7017041',
      costInHUF: '{{costInHUF}}',
      itemType: 'HardwareComponent',
      metadata: {
        FluidType: '{{fluidType}}',
        MaxPressure: '{{pressureLimitBar}} Bar (100 PSI)',
        CycleRate: 'Up to 600 shots/min',
        DispenseMode: 'Timed Mode / Continuous Steady Shot',
        VacuumSnuffBack: 'Adjustable 0 - 18 inHg Anti-Drip',
        TriggerInterface: '24V Opto-Isolated PLC I/O',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'sinumerik-cnc-runtime',
    name: 'Siemens Sinumerik CNC Core License',
    category: 'Software',
    icon: 'Code',
    description: 'CNC numerical kernel software license for multi-channel 5-axis milling, turning, and high-speed lookahead machining.',
    targetType: 'SoftwareComponent',
    tags: ['Siemens', 'Sinumerik', 'CNC', 'Software License', 'NCK'],
    variables: [
      {
        name: 'stationCode',
        label: 'CNC Machine Cell',
        type: 'string',
        defaultValue: 'CNC-01',
        required: true
      },
      {
        name: 'version',
        label: 'Sinumerik Version',
        type: 'string',
        defaultValue: 'V4.95 SP2'
      },
      {
        name: 'axisCount',
        label: 'Interpolated CNC Axes',
        type: 'number',
        defaultValue: 5
      },
      {
        name: 'costInHUF',
        label: 'License Cost (HUF)',
        type: 'number',
        defaultValue: 850000
      }
    ],
    template: {
      name: 'LIC-SINU-{{stationCode | uppercase}}-01',
      displayName: 'Sinumerik CNC Runtime Engine ({{stationCode | uppercase}})',
      technology: 'Siemens Sinumerik NCK Kernel',
      serialNumber: 'LIC-SINU-{{$randomHex}}',
      modelNumber: '6FC5800-0AP00-0YB0',
      costInHUF: '{{costInHUF}}',
      itemType: 'SoftwareComponent',
      metadata: {
        Version: '{{version}}',
        InterpolatedAxes: '{{axisCount}} Simultaneous Axes',
        LookAheadBlocks: '1000 Blocks High-Speed Kinematics',
        LicenseType: 'Hardware Dongle Secured / Node-Locked',
        OptionsPackage: 'ShopMill 3D Simulation & Advanced Surface',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'visionpro-ai-license',
    name: 'Cognex VisionPro Deep Learning AI Suite',
    category: 'Software',
    icon: 'Sparkles',
    description: 'Industrial AI computer vision software license for Red Analyze defect segmentation, Green Classify sorting, and Blue Locate character OCR.',
    targetType: 'SoftwareComponent',
    tags: ['Cognex', 'VisionPro', 'Deep Learning', 'AI', 'License'],
    variables: [
      {
        name: 'stationCode',
        label: 'Inspection Station',
        type: 'string',
        defaultValue: 'OP30',
        required: true
      },
      {
        name: 'licenseTier',
        label: 'License Tier',
        type: 'select',
        defaultValue: 'Inference + Runtime Acceleration (Floating)',
        options: [
          'Inference Only (Node-Locked USB Dongle)',
          'Inference + Training Studio (Workstation)',
          'Inference + Runtime Acceleration (Floating)'
        ]
      },
      {
        name: 'costInHUF',
        label: 'License Cost (HUF)',
        type: 'number',
        defaultValue: 1650000
      }
    ],
    template: {
      name: 'LIC-VPRO-{{stationCode | uppercase}}-01',
      displayName: 'Cognex VisionPro Deep Learning Suite ({{stationCode | uppercase}})',
      technology: 'Industrial Deep Learning Vision AI',
      serialNumber: 'LIC-VPRO-{{$randomHex}}',
      modelNumber: 'VPDL-RT-FLT-01',
      costInHUF: '{{costInHUF}}',
      itemType: 'SoftwareComponent',
      metadata: {
        SoftwareVersion: 'v3.2.1 LTS',
        Tier: '{{licenseTier}}',
        SupportedTools: 'Red Analyze, Green Classify, Blue Locate, Blue Read OCR',
        CUDAAcceleration: 'NVIDIA TensorRT 8.6 Optimized',
        LicenseServer: 'lic-serv.heimdall.internal:27000',
        Station: '{{stationCode | uppercase}}'
      }
    }
  },
  {
    id: 'kepserver-opcua',
    name: 'Kepware KEPServerEX OPC UA Gateway Server',
    category: 'Software',
    icon: 'Network',
    description: 'Industrial connectivity server translating legacy PLC protocols (Allen-Bradley DF1, Siemens S7, Modbus TCP) into unified OPC UA NodeSets.',
    targetType: 'SoftwareComponent',
    tags: ['Kepware', 'OPC UA', 'IoT Gateway', 'Industrial IT', 'SCADA'],
    variables: [
      {
        name: 'stationCode',
        label: 'Edge Gateway Host',
        type: 'string',
        defaultValue: 'GW-01',
        required: true
      },
      {
        name: 'maxTags',
        label: 'Max Tag Count License',
        type: 'select',
        defaultValue: '5,000 Tags Active Subscription',
        options: ['1,000 Tags', '5,000 Tags Active Subscription', 'Unlimited Enterprise']
      },
      {
        name: 'costInHUF',
        label: 'Software Cost (HUF)',
        type: 'number',
        defaultValue: 720000
      }
    ],
    template: {
      name: 'LIC-KEP-{{stationCode | uppercase}}-01',
      displayName: 'Kepware KEPServerEX OPC UA Server ({{stationCode | uppercase}})',
      technology: 'OPC UA Server & Protocol Gateway',
      serialNumber: 'LIC-KEP-{{$randomHex}}',
      modelNumber: 'KEP-EX-V6-ENTERPRISE',
      costInHUF: '{{costInHUF}}',
      itemType: 'SoftwareComponent',
      metadata: {
        ServerVersion: 'v6.14.263.0',
        TagCapacity: '{{maxTags}}',
        OPCUAPort: '49320',
        SecurityPolicies: 'Basic256Sha256 / Aes128_Sha256_RsaOaep',
        DriversLoaded: 'Siemens TCP/IP, Allen-Bradley ControlLogix, Modbus TCP, Beckhoff ADS',
        Station: '{{stationCode | uppercase}}'
      }
    }
  }
]
