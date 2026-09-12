#!/usr/bin/env python3
"""
Heimdall Unified Seed Data Pipeline & Integrity Validator
Generates enterprise-scale dataset with 100 diverse machines across 8 automated lines,
4 control topologies (1-1, 1-n, m-n, n-1), running TwinCAT 3 & MES clients,
550 serialized stock parts + bulk consumables, and 60 overtly fake, AI-generated users.
"""

import csv
import json
import os
import sys
import uuid
import random
import argparse

# Set seed for reproducible data generation
random.seed(42)

CSV_FILE = os.path.join(os.path.dirname(__file__), 'inventory_seed.csv')
SQL_FILE = os.path.join(os.path.dirname(__file__), 'incremental_seed.sql')
TOPOLOGY_FILE = os.path.join(os.path.dirname(__file__), 'production_topology.json')

MANUFACTURERS = [
    "Siemens", "Beckhoff", "Fanuc", "KUKA", "Cognex", "Keyence", "Festo", "Omron",
    "Atlas Copco", "Bosch Rexroth", "Trumpf", "TOX Pressotechnik", "Nordson", "SICK",
    "SEW Eurodrive", "Balluff", "Phoenix Contact", "Advantech", "Würth", "Hirschmann"
]
SUPPLIERS = ["Insight Industrial", "Direct Automation Europe", "Farnell Components", "RS Components", "MISUMI Industrial", "Conrad Electronic"]
TEAMS = [
    "Controls Engineering (Synthetic AI Guild)",
    "Robotics & Cybernetics (Synthetic AI Guild)",
    "Vision & Photons (Synthetic AI Guild)",
    "SMT & Microchips (Synthetic AI Guild)",
    "Tooling & Mechanoids (Synthetic AI Guild)",
    "Plant Maintenance (Synthetic AI Guild)",
    "Platform Operations (Synthetic AI Guild)"
]

ORGANIZATIONS = [
    # 8 Production Lines
    {"id": "org-line-01", "name": "Line 01 – Synthetic Audi E-Tron Module Line", "slug": "line-01-synthetic-audi-e-tron-module-line", "desc": "High-voltage battery module automated assembly loop"},
    {"id": "org-line-02", "name": "Line 02 – Synthetic Audi Battery Pack Line", "slug": "line-02-synthetic-audi-battery-pack-line", "desc": "Pack integration, adhesive sealing, and structural bolting"},
    {"id": "org-line-03", "name": "Line 03 – Synthetic Audi Powertrain Line", "slug": "line-03-synthetic-audi-powertrain-line", "desc": "Stator hairpin winding, rotor insertion, and dyno test"},
    {"id": "org-line-04", "name": "Line 04 – Synthetic Electronics SMT Placement", "slug": "line-04-synthetic-electronics-smt-placement", "desc": "High-speed dual-beam SMT placement and 3D SPI/AOI"},
    {"id": "org-line-05", "name": "Line 05 – Synthetic Body-in-White Robotic Welding", "slug": "line-05-synthetic-body-in-white-robotic-welding", "desc": "Subframe clamping, spot welding, and laser seam joining"},
    {"id": "org-line-06", "name": "Line 06 – Synthetic Precision Press Fit", "slug": "line-06-synthetic-precision-press-fit", "desc": "Servo press bushing insertion and force-displacement monitor"},
    {"id": "org-line-07", "name": "Line 07 – Synthetic Optical Quality Metrology", "slug": "line-07-synthetic-optical-quality-metrology", "desc": "Telecentric multi-camera metrology and defect classification"},
    {"id": "org-line-08", "name": "Line 08 – Synthetic End-of-Line Vehicle Integration", "slug": "line-08-synthetic-end-of-line-vehicle-integration", "desc": "Final brake dyno, radar ADAS calibration, and OBD-II flash"},
    # 7 Technology Guilds
    {"id": "org-controls", "name": "Controls & Automation (Synthetic AI Guild)", "slug": "controls-automation-synthetic-ai-guild", "desc": "PLC controls, TwinCAT 3, EtherCAT, and fieldbus automation"},
    {"id": "org-robotics", "name": "Robotics & Cybernetics (Synthetic AI Guild)", "slug": "robotics-cybernetics-synthetic-ai-guild", "desc": "6-axis articulated robots, Cartesian gantries, and motion kinematics"},
    {"id": "org-vision", "name": "Vision & Photons (Synthetic AI Guild)", "slug": "vision-photons-synthetic-ai-guild", "desc": "Industrial optical inspection, deep learning vision, and laser profilometry"},
    {"id": "org-smt", "name": "SMT & Microchips (Synthetic AI Guild)", "slug": "smt-microchips-synthetic-ai-guild", "desc": "Surface mount technology, PCB testing, and reflow processes"},
    {"id": "org-assembly", "name": "Tooling & Mechanoids (Synthetic AI Guild)", "slug": "tooling-mechanoids-synthetic-ai-guild", "desc": "Conveyors, pallets, pneumatic grippers, and torque nutrunners"},
    {"id": "org-maintenance", "name": "Plant Maintenance (Synthetic AI Guild)", "slug": "plant-maintenance-synthetic-ai-guild", "desc": "Predictive maintenance, depot repairs, and spare parts management"},
    {"id": "org-platform", "name": "Platform Operations (Synthetic AI Guild)", "slug": "platform-operations-synthetic-ai-guild", "desc": "Root platform governance, IT infrastructure, and PKI security"},
    # 1 Cross-Project Group
    {"id": "org-audi-proj", "name": "Audi Vehicle Project (Synthetic AI Guild)", "slug": "audi-vehicle-project-synthetic-ai-guild", "desc": "Cross-line engineering and product lifecycle management"}
]

# 60 OVERTLY FAKE, AI-GENERATED USERS
FAKE_USERS = [
    ("usr-synth-01", "Synthetica Botman (AI Model v4)", "synthetica.botman.ai@fake-factory.internal", "controls_engineer", "Lead Controls Specialist", "Controls & Automation", ["org-controls", "org-line-01", "org-audi-proj"]),
    ("usr-synth-02", "Robo McControlsFace", "robo.mccontrolsface.ai@fake-factory.internal", "controls_engineer", "Senior PLC Engineer", "Controls & Automation", ["org-controls", "org-line-01", "org-line-02"]),
    ("usr-synth-03", "Dr. Algorithmus Prime", "dr.algorithmus.prime.ai@fake-factory.internal", "plant_director", "Synthetic Plant Overlord", "Plant Management", ["org-platform", "org-audi-proj", "org-line-01", "org-line-02", "org-controls"]),
    ("usr-synth-04", "Tensor Flowski", "tensor.flowski.ai@fake-factory.internal", "lead_engineer", "Machine Learning Vision Lead", "Vision Systems", ["org-vision", "org-line-04", "org-line-07", "org-audi-proj"]),
    ("usr-synth-05", "Vectoria Embeddings", "vectoria.embeddings.ai@fake-factory.internal", "engineer", "Precision Metrology Engineer", "Vision Systems", ["org-vision", "org-line-07"]),
    ("usr-synth-06", "Claude Von Tokenizer", "claude.vontokenizer.ai@fake-factory.internal", "plant_engineering_manager", "Chief Automation Architect", "Plant Engineering", ["org-platform", "org-audi-proj", "org-controls", "org-robotics"]),
    ("usr-synth-07", "Promptly GenAI-Smith", "promptly.genai.ai@fake-factory.internal", "operative_planner", "Operative Line Planner", "Operations Planning", ["org-line-01", "org-line-02", "org-line-03", "org-platform"]),
    ("usr-synth-08", "Nullpointer McException", "nullpointer.mcexception.ai@fake-factory.internal", "it_site_admin", "Senior Bug Hunter & IT Admin", "Industrial IT", ["org-platform", "org-controls"]),
    ("usr-synth-09", "Hal Nine-Thousand-B", "hal9000b.ai@fake-factory.internal", "shift_leader", "Automated Safety Supervisor", "Safety & EHS", ["org-platform", "org-line-05"]),
    ("usr-synth-10", "Otto Mation", "otto.mation.ai@fake-factory.internal", "engineer", "Robotic Cell Integrator", "Robotics", ["org-robotics", "org-line-01", "org-line-05"]),
    ("usr-synth-11", "Circuit Breaker Johnson", "circuit.breaker.ai@fake-factory.internal", "technician", "High-Voltage Electrical Tech", "Maintenance", ["org-maintenance", "org-line-02"]),
    ("usr-synth-12", "Bytecode Beauregard", "bytecode.beauregard.ai@fake-factory.internal", "engineer", "Embedded Firmware Hacker", "Industrial IT", ["org-controls", "org-smt"]),
    ("usr-synth-13", "Overfitted McValidation", "overfitted.mcvalidation.ai@fake-factory.internal", "engineer", "Quality Assurance Inspector", "Quality", ["org-vision", "org-line-07", "org-line-08"]),
    ("usr-synth-14", "Stochastic Parrot-Perez", "stochastic.parrot.ai@fake-factory.internal", "technician", "Dispatch Coordinator", "Logistics", ["org-platform", "org-line-01", "org-line-08"]),
    ("usr-synth-15", "Rusty Cogsworth", "rusty.cogsworth.ai@fake-factory.internal", "technician", "Mechanical Tooling Master", "Maintenance", ["org-maintenance", "org-assembly", "org-line-06"]),
    ("usr-synth-16", "Deeplearnington Smyth", "deeplearnington.smyth.ai@fake-factory.internal", "engineer", "Neural Vision Developer", "Vision Systems", ["org-vision", "org-line-04"]),
    ("usr-synth-17", "Silicon O'Chip", "silicon.ochip.ai@fake-factory.internal", "engineer", "SMT Placement Specialist", "SMT & Microchips", ["org-smt", "org-line-04"]),
    ("usr-synth-18", "Glitchy McGlitch", "glitchy.mcglitch.ai@fake-factory.internal", "technician", "Diagnostic Test Operator", "Test", ["org-maintenance", "org-line-08"]),
    ("usr-synth-19", "Matrix O'Gradient", "matrix.ogradient.ai@fake-factory.internal", "engineer", "Optimization Mathematical Modeler", "Plant Engineering", ["org-controls", "org-audi-proj"]),
    ("usr-synth-20", "Bitty Byte-Bender", "bitty.bytebender.ai@fake-factory.internal", "technician", "Fieldbus Wiring Specialist", "Controls & Automation", ["org-controls", "org-line-03"]),
    ("usr-synth-21", "Epoch MacEpochface", "epoch.macepochface.ai@fake-factory.internal", "engineer", "Reflow Thermal Profiler", "SMT & Microchips", ["org-smt", "org-line-04"]),
    ("usr-synth-22", "Transformer D. Model", "transformer.d.model.ai@fake-factory.internal", "lead_engineer", "Multi-Agent Fleet Coordinator", "Robotics", ["org-robotics", "org-line-02", "org-line-05"]),
    ("usr-synth-23", "Cyberia Glitchcraft", "cyberia.glitchcraft.ai@fake-factory.internal", "technician", "Laser Optics Calibration Tech", "Tooling", ["org-assembly", "org-line-01", "org-line-05"]),
    ("usr-synth-24", "Robo-Copernicus", "robo.copernicus.ai@fake-factory.internal", "engineer", "Astronomical Motion Kinematicist", "Robotics", ["org-robotics", "org-audi-proj"]),
    ("usr-synth-25", "Synthia Hal-Zero", "synthia.halzero.ai@fake-factory.internal", "engineer", "2K Thermal Paste Formulator", "Dispensing", ["org-assembly", "org-line-01", "org-line-02"]),
    ("usr-synth-26", "Gepetto Automaton", "gepetto.automaton.ai@fake-factory.internal", "technician", "Conveyor Pallet Mechanic", "Tooling", ["org-assembly", "org-line-01", "org-line-03"]),
    ("usr-synth-27", "Perceptron Jones", "perceptron.jones.ai@fake-factory.internal", "technician", "Sensor Calibration Specialist", "Maintenance", ["org-maintenance", "org-line-06"]),
    ("usr-synth-28", "Automata Sparkplug", "automata.sparkplug.ai@fake-factory.internal", "technician", "Capacitor Discharge Tech", "Maintenance", ["org-maintenance", "org-line-02"]),
    ("usr-synth-29", "Bitbucket O'Flanagan", "bitbucket.oflanagan.ai@fake-factory.internal", "engineer", "PLC Git Versioning Engineer", "Industrial IT", ["org-controls", "org-platform"]),
    ("usr-synth-30", "Pixelina Subpixel", "pixelina.subpixel.ai@fake-factory.internal", "engineer", "Optical Lens Inspector", "Vision Systems", ["org-vision", "org-line-07"]),
    ("usr-synth-31", "Screwy McTorque", "screwy.mctorque.ai@fake-factory.internal", "technician", "Atlas Copco Calibration Tech", "Tooling", ["org-assembly", "org-line-01", "org-line-06"]),
    ("usr-synth-32", "Pneumatica Flow", "pneumatica.flow.ai@fake-factory.internal", "technician", "Festo Valve Terminal Tuner", "Maintenance", ["org-maintenance", "org-line-06"]),
    ("usr-synth-33", "Laserbeam Larry", "laserbeam.larry.ai@fake-factory.internal", "engineer", "Trumpf Laser Specialist", "Welding", ["org-assembly", "org-line-01", "org-line-05"]),
    ("usr-synth-34", "Relay McSolenoid", "relay.mcsolenoid.ai@fake-factory.internal", "technician", "Emergency Stop Loop Certifier", "Safety", ["org-controls", "org-line-05"]),
    ("usr-synth-35", "Dataframe Doris", "dataframe.doris.ai@fake-factory.internal", "engineer", "Telemetry Stream Aggregator", "Industrial IT", ["org-platform", "org-audi-proj"]),
    ("usr-synth-36", "Backprop Barnaby", "backprop.barnaby.ai@fake-factory.internal", "engineer", "Error Propagation Minimizer", "Quality", ["org-vision", "org-line-07"]),
    ("usr-synth-37", "Firmware Floyd", "firmware.floyd.ai@fake-factory.internal", "technician", "ADS Beckhoff Flasher", "Controls & Automation", ["org-controls", "org-line-03"]),
    ("usr-synth-38", "Logicgate Lucy", "logicgate.lucy.ai@fake-factory.internal", "engineer", "IEC 61131-3 Ladder Specialist", "Controls & Automation", ["org-controls", "org-line-02"]),
    ("usr-synth-39", "Heatsink Hank", "heatsink.hank.ai@fake-factory.internal", "technician", "Battery Thermal Interface Tech", "Assembly", ["org-assembly", "org-line-02"]),
    ("usr-synth-40", "Actuator Artie", "actuator.artie.ai@fake-factory.internal", "technician", "Servo Press Load Cell Tech", "Tooling", ["org-assembly", "org-line-06"]),
    ("usr-synth-41", "Ethercat Emma", "ethercat.emma.ai@fake-factory.internal", "engineer", "Distributed Clock Synchronizer", "Controls & Automation", ["org-controls", "org-line-01", "org-audi-proj"]),
    ("usr-synth-42", "Profibus Pete", "profibus.pete.ai@fake-factory.internal", "technician", "Legacy RS-485 Cable Wrangler", "Maintenance", ["org-maintenance", "org-line-05"]),
    ("usr-synth-43", "Opcua Oliver", "opcua.oliver.ai@fake-factory.internal", "engineer", "NodeSet Companion Model Expert", "Industrial IT", ["org-platform", "org-controls"]),
    ("usr-synth-44", "Barcode Brenda", "barcode.brenda.ai@fake-factory.internal", "technician", "DataMatrix 2D Scanner Tester", "Quality", ["org-vision", "org-line-04"]),
    ("usr-synth-45", "Solderpot Sammy", "solderpot.sammy.ai@fake-factory.internal", "technician", "Wave Solder Nitrogen Overseer", "SMT & Microchips", ["org-smt", "org-line-04"]),
    ("usr-synth-46", "Bushing Barney", "bushing.barney.ai@fake-factory.internal", "technician", "Interference Fit Specialist", "Assembly", ["org-assembly", "org-line-06"]),
    ("usr-synth-47", "Leakcheck Lola", "leakcheck.lola.ai@fake-factory.internal", "engineer", "Helium Sniffer Chamber Lead", "Test", ["org-maintenance", "org-line-02"]),
    ("usr-synth-48", "Voltmeter Victor", "voltmeter.victor.ai@fake-factory.internal", "technician", "Hi-Pot Electrical Tester", "Test", ["org-maintenance", "org-line-01", "org-line-08"]),
    ("usr-synth-49", "Dynamo Dan", "dynamo.dan.ai@fake-factory.internal", "engineer", "Roll Bench Dyno Specialist", "Test", ["org-assembly", "org-line-08"]),
    ("usr-synth-50", "Radar Rhonda", "radar.rhonda.ai@fake-factory.internal", "engineer", "ADAS Target Array Aligner", "Test", ["org-vision", "org-line-08"]),
    ("usr-synth-51", "Torquewrench Tim", "torquewrench.tim.ai@fake-factory.internal", "technician", "Angle-Over-Yield Calibrator", "Assembly", ["org-assembly", "org-line-03"]),
    ("usr-synth-52", "Gantry Gary", "gantry.gary.ai@fake-factory.internal", "technician", "XYZ Overhead Cartesian Rigger", "Tooling", ["org-robotics", "org-line-02"]),
    ("usr-synth-53", "Optical Olivia", "optical.olivia.ai@fake-factory.internal", "engineer", "Telecentric Lighting Tuner", "Vision Systems", ["org-vision", "org-line-07"]),
    ("usr-synth-54", "Hairpin Harold", "hairpin.harold.ai@fake-factory.internal", "technician", "Copper Hairpin Bending Master", "Assembly", ["org-assembly", "org-line-03"]),
    ("usr-synth-55", "Impregnator Ian", "impregnator.ian.ai@fake-factory.internal", "technician", "Resin Varnish Dipping Operator", "Assembly", ["org-assembly", "org-line-03"]),
    ("usr-synth-56", "Clamping Clara", "clamping.clara.ai@fake-factory.internal", "technician", "Hydraulic BIW Jig Operator", "Tooling", ["org-assembly", "org-line-05"]),
    ("usr-synth-57", "Interlock Irma", "interlock.irma.ai@fake-factory.internal", "technician", "Safety Light Curtain Inspector", "Maintenance", ["org-maintenance", "org-line-06"]),
    ("usr-synth-58", "Obdflash Oscar", "obdflash.oscar.ai@fake-factory.internal", "engineer", "UDS Diagnostic ECU Flasher", "Industrial IT", ["org-platform", "org-line-08"]),
    ("usr-synth-59", "Washgate Wanda", "washgate.wanda.ai@fake-factory.internal", "technician", "High-Pressure De-Ionized Washer", "Maintenance", ["org-maintenance", "org-line-08"]),
    ("usr-synth-60", "Watchdog Walter", "watchdog.walter.ai@fake-factory.internal", "system_admin", "Master Watchdog & Superuser", "Platform Operations", ["org-platform", "org-audi-proj", "org-controls", "org-maintenance"])
]

# LINE METADATA CONFIGURATION (8 LINES, 100 STATIONS TOTAL)
LINE_CONFIGS = [
    {
        "line_num": 1,
        "org_id": "org-line-01",
        "name": "Line 01 – Synthetic Audi E-Tron Module Line (AI Sim)",
        "tech": "Dispensing",
        "station_count": 12,
        "stations": [
            ("OP010", "Hyper-Conveyor Pallet Infeed 9000", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L01-OP010"),
            ("OP020", "Robo-RFID Pallet Scanner AI-X", "ConveyorTransfer", "Test", "Balluff", "L01-OP020"),
            ("OP030", "Giga-Gluer 2K Thermal Dispenser Bot", "Dispenser", "Dispensing", "Nordson", "L01-OP030"),
            ("OP040", "Auto-Bolt Torquinator 3000", "FasteningStation", "Fastening", "Atlas Copco", "L01-OP040"),
            ("OP050", "Pallet-Lift Elevator Mech-Tron", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L01-OP050"),
            ("OP060", "Laser-Zapper Seam Welder Omni-9", "LaserWelder", "Welding", "Trumpf", "L01-OP060"),
            ("OP070", "High-Voltage Sparky Insulation Tester", "EOLTester", "Test", "Chroma", "L01-OP070"),
            ("OP080", "Cell-Loader Gantry Manipulator AI", "RobotCell", "Robotics", "KUKA", "L01-OP080"),
            ("OP090", "Deep-Vision AOI Flaw-Finder 4000", "VisionInspection", "Test", "Cognex", "L01-OP090"),
            ("OP100", "Multi-Spindle Cover Fastener Cyber", "FasteningStation", "Fastening", "Atlas Copco", "L01-OP100"),
            ("OP110", "Pallet Accumulator Buffer Loop-1", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L01-OP110"),
            ("OP120", "Outfeed Barcode Check & Gate Bot", "ConveyorTransfer", "Test", "Keyence", "L01-OP120")
        ]
    },
    {
        "line_num": 2,
        "org_id": "org-line-02",
        "name": "Line 02 – Synthetic Audi Battery Pack Line (AI Sim)",
        "tech": "Assembly",
        "station_count": 13,
        "stations": [
            ("OP010", "Pack-Tray Roller Infeed Infeed-Bot", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L02-OP010"),
            ("OP020", "Cooling-Plate Adhesive Applicator AI", "Dispenser", "Dispensing", "Nordson", "L02-OP020"),
            ("OP030", "Heavy-Module Unload Robot Gantry 1", "RobotCell", "Robotics", "Fanuc", "L02-OP030"),
            ("OP040", "Dual-Arm Battery Tray Inserter Mech", "RobotCell", "Robotics", "Fanuc", "L02-OP040"),
            ("OP050", "Busbar Torque Tightening Station 8X", "FasteningStation", "Fastening", "Atlas Copco", "L02-OP050"),
            ("OP060", "Laser Seam Seal Chamber Hermetic-X", "LaserWelder", "Welding", "Trumpf", "L02-OP060"),
            ("OP070", "Laser Bead Profilometer Checker AI", "VisionInspection", "Test", "Cognex", "L02-OP070"),
            ("OP080", "Helium Leak Sniffer Chamber Zero-P", "EOLTester", "Test", "Pfeiffer", "L02-OP080"),
            ("OP090", "Pack Lid Structural Servo Press 50kN", "ServoPress", "Fastening", "TOX Pressotechnik", "L02-OP090"),
            ("OP100", "Perimeter Hex Bolt Nutrunner Gantry", "FasteningStation", "Fastening", "Atlas Copco", "L02-OP100"),
            ("OP110", "Optical Topographic Seal Inspector", "VisionInspection", "Test", "Keyence", "L02-OP110"),
            ("OP120", "Final Pack Discharge Pallet Elevator", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L02-OP120"),
            ("OP130", "Battery Pack EOL High-Voltage Gate", "EOLTester", "Test", "Chroma", "L02-OP130")
        ]
    },
    {
        "line_num": 3,
        "org_id": "org-line-03",
        "name": "Line 03 – Synthetic Audi Powertrain Line (AI Sim)",
        "tech": "Assembly",
        "station_count": 12,
        "stations": [
            ("OP010", "Hairpin Stator Raw Infeed Track", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L03-OP010"),
            ("OP020", "Precision Stator Iron Core Press 80kN", "ServoPress", "Assembly", "TOX Pressotechnik", "L03-OP020"),
            ("OP030", "Hairpin Wire Crown Insertion Robot", "RobotCell", "Robotics", "KUKA", "L03-OP030"),
            ("OP040", "Crown Head Twist & Chamfer Tooling", "FasteningStation", "Fastening", "Atlas Copco", "L03-OP040"),
            ("OP050", "Rotor Shaft Cryogenic Insertion Press", "ServoPress", "Assembly", "TOX Pressotechnik", "L03-OP050"),
            ("OP060", "Trickle Resin Trickler & Varnish Bot", "Dispenser", "Dispensing", "Nordson", "L03-OP060"),
            ("OP070", "Infrared Resin Curing Tunnel EOL", "EOLTester", "Test", "Heraeus", "L03-OP070"),
            ("OP080", "High-Speed Dynamic Balancing Rig 3D", "VisionInspection", "Test", "Schenck", "L03-OP080"),
            ("OP090", "End-Shield Bearing Fastener Torquer", "FasteningStation", "Fastening", "Atlas Copco", "L03-OP090"),
            ("OP100", "Stator Rotor Magnetization Gate AI", "RobotCell", "Robotics", "KUKA", "L03-OP100"),
            ("OP110", "Powertrain Conveyor Turn-Table Loop", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L03-OP110"),
            ("OP120", "Powertrain Full Dynamometer Dyno-X", "EOLTester", "Test", "AVL", "L03-OP120")
        ]
    },
    {
        "line_num": 4,
        "org_id": "org-line-04",
        "name": "Line 04 – Synthetic Electronics SMT Placement (AI Sim)",
        "tech": "SMT",
        "station_count": 13,
        "stations": [
            ("OP010", "SMT Magazine Unloader Pallet Shuttle", "ConveyorTransfer", "Assembly", "ASM", "L04-OP010"),
            ("OP020", "DEK Solder Paste Jet Printer 0.1mm", "VisionInspection", "Test", "ASM", "L04-OP020"),
            ("OP030", "Koh Young 3D Solder Paste Inspector", "VisionInspection", "Test", "Koh Young", "L04-OP030"),
            ("OP040", "Siplace Dual-Gantry Chip Shooter AI", "RobotCell", "Robotics", "ASM", "L04-OP040"),
            ("OP050", "Odd-Form Component Inserter Gantry", "Dispenser", "Dispensing", "Fuji", "L04-OP050"),
            ("OP060", "Pre-Reflow 3D Optical Vision Checker", "VisionInspection", "Test", "Cognex", "L04-OP060"),
            ("OP070", "10-Zone Nitrogen Reflow Convection Oven", "EOLTester", "Test", "Heller", "L04-OP070"),
            ("OP080", "Post-Reflow Automated Optical Inspector", "VisionInspection", "Test", "Koh Young", "L04-OP080"),
            ("OP090", "Flying-Probe ICT Circuit Board Tester", "EOLTester", "Test", "Spea", "L04-OP090"),
            ("OP100", "Selective Wave Solder Mini-Pot Bot", "FasteningStation", "Welding", "Ersa", "L04-OP100"),
            ("OP110", "Conformal Coating UV Dispenser Booth", "Dispenser", "Dispensing", "Nordson", "L04-OP110"),
            ("OP120", "UV Cure Inspection Tunnel & Camera", "VisionInspection", "Test", "Cognex", "L04-OP120"),
            ("OP130", "PCB Depaneling Laser Router Outfeed", "ConveyorTransfer", "Assembly", "LPKF", "L04-OP130")
        ]
    },
    {
        "line_num": 5,
        "org_id": "org-line-05",
        "name": "Line 05 – Synthetic Body-in-White Robotic Welding (AI Sim)",
        "tech": "Welding",
        "station_count": 12,
        "stations": [
            ("OP010", "Subframe Clamping Jig Shuttle Table", "ConveyorTransfer", "Assembly", "KUKA", "L05-OP010"),
            ("OP020", "Heavy Spot-Welding Robot Titan-01", "RobotCell", "Robotics", "KUKA", "L05-OP020"),
            ("OP030", "Trumpf Disk Laser Welding Chamber A", "LaserWelder", "Welding", "Trumpf", "L05-OP030"),
            ("OP040", "Robotic Stud Welding Manipulator B", "RobotCell", "Robotics", "Fanuc", "L05-OP040"),
            ("OP050", "Blind Rivet Nut Insertion Screwer", "FasteningStation", "Fastening", "Atlas Copco", "L05-OP050"),
            ("OP060", "Laser Seam Geometry Tracker Scanner", "LaserWelder", "Welding", "Trumpf", "L05-OP060"),
            ("OP070", "Optical Body Gap & Flushness Gate", "VisionInspection", "Test", "Cognex", "L05-OP070"),
            ("OP080", "Structural Foam Injection Applicator", "Dispenser", "Dispensing", "Nordson", "L05-OP080"),
            ("OP090", "Friction Stir Welder Overhead Unit", "FasteningStation", "Welding", "KUKA", "L05-OP090"),
            ("OP100", "Dual-Robot Seam Finishing Cell", "RobotCell", "Robotics", "Fanuc", "L05-OP100"),
            ("OP110", "Overhead Transfer Crane Pick Shifter", "ConveyorTransfer", "Assembly", "Demag", "L05-OP110"),
            ("OP120", "Body Shell Dimension Verification Gate", "VisionInspection", "Test", "Keyence", "L05-OP120")
        ]
    },
    {
        "line_num": 6,
        "org_id": "org-line-06",
        "name": "Line 06 – Synthetic Precision Press Fit (AI Sim)",
        "tech": "Fastening",
        "station_count": 12,
        "stations": [
            ("OP010", "Bearing Housing Pallet Shuttle Infeed", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L06-OP010"),
            ("OP020", "TOX Electric Servo Press 100kN Unit 1", "ServoPress", "Fastening", "TOX Pressotechnik", "L06-OP020"),
            ("OP030", "Bushing Rotary Feeder & Inserter 2", "ServoPress", "Fastening", "TOX Pressotechnik", "L06-OP030"),
            ("OP040", "Needle Bearing Pick-and-Place Robot", "RobotCell", "Robotics", "KUKA", "L06-OP040"),
            ("OP050", "Threaded Retainer Torque Tightener", "FasteningStation", "Fastening", "Atlas Copco", "L06-OP050"),
            ("OP060", "High-Precision Pin Press Servo 25kN", "ServoPress", "Fastening", "TOX Pressotechnik", "L06-OP060"),
            ("OP070", "Force-Displacement Envelope Monitor", "VisionInspection", "Test", "Promess", "L06-OP070"),
            ("OP080", "Ultrasonic Crack & Defect Detector", "EOLTester", "Test", "Olympus", "L06-OP080"),
            ("OP090", "Circlip Snap-Ring Pneumatic Press", "ServoPress", "Fastening", "Festo", "L06-OP090"),
            ("OP100", "Bearing Lubricant Micro-Doser 10mg", "Dispenser", "Dispensing", "Nordson", "L06-OP100"),
            ("OP110", "Quality Pass Diverter Rejection Gate", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L06-OP110"),
            ("OP120", "Finished Bearing Pallet Packing Station", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L06-OP120")
        ]
    },
    {
        "line_num": 7,
        "org_id": "org-line-07",
        "name": "Line 07 – Synthetic Optical Quality Metrology (AI Sim)",
        "tech": "Test",
        "station_count": 13,
        "stations": [
            ("OP010", "Inspection Pallet Indexing Rotary Bed", "ConveyorTransfer", "Assembly", "Weiss", "L07-OP010"),
            ("OP020", "Telecentric 50MP Multi-Angle Booth", "VisionInspection", "Test", "Cognex", "L07-OP020"),
            ("OP030", "Laser Line Profilometer 3D Surface AI", "VisionInspection", "Test", "Keyence", "L07-OP030"),
            ("OP040", "Surface Roughness Tactile Probing Arm", "RobotCell", "Robotics", "Mitutoyo", "L07-OP040"),
            ("OP050", "Specular Reflectance Defect Scanner", "VisionInspection", "Test", "Cognex", "L07-OP050"),
            ("OP060", "Optical Coordinate Measuring CMM Gantry", "EOLTester", "Test", "Zeiss", "L07-OP060"),
            ("OP070", "Deep-Learning Anomaly Classifier AI", "VisionInspection", "Test", "Cognex", "L07-OP070"),
            ("OP080", "Part Re-Orientation Robotic Swivel", "RobotCell", "Robotics", "KUKA", "L07-OP080"),
            ("OP090", "X-Ray CT Void Inspection Scanner", "VisionInspection", "Test", "Yxlon", "L07-OP090"),
            ("OP100", "Direct Part Marking Laser QR Stamper", "FasteningStation", "Welding", "Trumpf", "L07-OP100"),
            ("OP110", "Post-Marking Verification Scanner 2D", "VisionInspection", "Test", "Cognex", "L07-OP110"),
            ("OP120", "Certified Quality Pallet Stacking Bay", "ConveyorTransfer", "Assembly", "Demag", "L07-OP120"),
            ("OP130", "Automated Metrology Archive & Gate", "ConveyorTransfer", "Test", "Keyence", "L07-OP130")
        ]
    },
    {
        "line_num": 8,
        "org_id": "org-line-08",
        "name": "Line 08 – Synthetic End-of-Line Vehicle Integration (AI Sim)",
        "tech": "Test",
        "station_count": 13,
        "stations": [
            ("OP010", "Final Chasis Docking & Lock Conveyor", "ConveyorTransfer", "Assembly", "Siemens", "L08-OP010"),
            ("OP020", "Brake Hydraulic Vacuum Bleed Bench", "EOLTester", "Test", "Dürr", "L08-OP020"),
            ("OP030", "Coolant Degassing & Fluid Filling Station", "EOLTester", "Test", "Dürr", "L08-OP030"),
            ("OP040", "Wheel Nut Automated Torque Tightener 5X", "RobotCell", "Robotics", "Atlas Copco", "L08-OP040"),
            ("OP050", "Optical Wheel Alignment Laser Gate", "VisionInspection", "Test", "Beissbarth", "L08-OP050"),
            ("OP060", "Headlight Matrix LED Calibration Rig", "EOLTester", "Test", "Hella", "L08-OP060"),
            ("OP070", "ADAS Radar & LiDAR Target Array Aligner", "FasteningStation", "Test", "Continental", "L08-OP070"),
            ("OP080", "All-Wheel Drive Roll Dynamometer 150kW", "EOLTester", "Test", "Maha", "L08-OP080"),
            ("OP090", "OBD-II High-Speed Ethernet Flash ECU", "RobotCell", "Robotics", "Vector", "L08-OP090"),
            ("OP100", "Underbody Acoustic Ultrasonic Sniffer", "VisionInspection", "Test", "Siemens", "L08-OP100"),
            ("OP110", "Monsoon Water Ingress Leak Test Booth", "EOLTester", "Test", "Dürr", "L08-OP110"),
            ("OP120", "Hot Air Blow-Off & Drying Tunnel Bot", "ConveyorTransfer", "Assembly", "Dürr", "L08-OP120"),
            ("OP130", "Final Shipping Factory Release Gate", "ConveyorTransfer", "Test", "Siemens", "L08-OP130")
        ]
    }
]

def generate_csv(output_path=CSV_FILE):
    print("Generating enterprise inventory dataset (100 Diverse Machines across 8 Lines, 4 Topologies, 550 Serialized Stock, Bulk Stock)...")
    rows = []

    # 1. Generate Exactly 100 Diverse Machines
    stations = []
    total_machine_count = 0

    for l_cfg in LINE_CONFIGS:
        line_name = l_cfg["name"]
        org_id = l_cfg["org_id"]
        for op_code, disp_label, m_type, tech, mfr, handle in l_cfg["stations"]:
            total_machine_count += 1
            st_id = f"LINE-{l_cfg['line_num']:02d}-{op_code}"
            st_name = f"L{l_cfg['line_num']:02d}-{op_code}"
            st_display = f"{st_name} – {disp_label}"
            team = random.choice(TEAMS)
            sup = random.choice(SUPPLIERS)
            sn = f"SN-SYNTH-MCH-{l_cfg['line_num']:02d}-{op_code}"

            meta = {
                "Line": line_name,
                "Technology": tech,
                "MachineType": m_type,
                "StationIdentifier": st_id,
                "CycleTimeTarget": f"{random.randint(15, 60)}s",
                "SafetyRating": random.choice(["SIL3 / PLe", "SIL2 / PLd"]),
                "PowerSupply": random.choice(["400V 3Ph 50Hz", "24V DC Industrial Spool", "48V High Current"]),
                "ConveyorLoop": f"Conveyor Loop L{l_cfg['line_num']:02d}",
                "PalletCarrierType": "Pallet RFID High-Speed 400x400"
            }

            stations.append({
                "Type": "Machine",
                "Name": st_name,
                "DisplayName": st_display,
                "ResponsibleTeam": team,
                "Manufacturer": mfr,
                "Supplier": sup,
                "SerialNumber": sn,
                "ParentName": "",
                "StationIdentifier": st_id,
                "ClientPcHostname": "",
                "Metadata": json.dumps(meta),
                "PinnedObjectHandle": handle,
                "OrganizationId": org_id,
                "StorageLocation": f"{line_name} – Cell {op_code}",
                "EquipmentStatus": "InMachine",
                "MachineId": "",
                "StockQuantity": "",
                "MinStockThreshold": "",
                "IsStockItem": "False",
                "Technology": tech,
                "MachineType": m_type,
                "GroupId": line_name
            })

    assert total_machine_count == 100, f"Expected 100 machines, got {total_machine_count}"
    rows.extend(stations)

    # 2. Generate IPC Controllers & Control Topologies (1-1, 1-n, m-n, n-1)
    # Every line will feature:
    # - 1-1: Dedicated IPC to single machine (e.g. OP030)
    # - 1-n: 1 Central IPC to multiple conveyor/buffer stations (e.g. OP010, OP020, OP050)
    # - m-n: Multiple IPCs controlling multiple coordinated stations (e.g. 2 IPCs to OP080 & OP090)
    # - n-1: Multiple specialized IPCs on 1 complex station (e.g. PLC + Vision + MES on OP060)
    pcs = []
    pc_station_links = [] # (pc_name, station_name, role)

    pc_idx = 1
    for l_cfg in LINE_CONFIGS:
        l_num = l_cfg["line_num"]
        l_name = l_cfg["name"]
        org_id = l_cfg["org_id"]

        # A. 1-1 Topology: Dedicated IPC on OP030
        ipc_11 = f"IPC-L{l_num:02d}-OP030-DEDICATED"
        pcs.append({
            "hostname": ipc_11,
            "display": f"{ipc_11} (Dedicated 1:1 Controller)",
            "station": f"L{l_num:02d}-OP030",
            "org": org_id,
            "mfr": "Beckhoff",
            "os": "Windows 10 IoT Enterprise 2021 LTSC",
            "twincat": f"TC3_Line{l_num:02d}_Cell030.tsproj (Port 851)",
            "mes": "Audi MES Production Client v3.1",
            "model": "Embedded PC C6030 Fanless"
        })
        pc_station_links.append((ipc_11, f"L{l_num:02d}-OP030", "Primary"))

        # B. 1-n Topology: 1 Main Conveyor IPC controlling OP010, OP020, OP050, OP110
        ipc_1n = f"IPC-L{l_num:02d}-CONVEYOR-MAIN"
        pcs.append({
            "hostname": ipc_1n,
            "display": f"{ipc_1n} (Master Conveyor 1:n PLC)",
            "station": f"L{l_num:02d}-OP010",
            "org": org_id,
            "mfr": "Siemens",
            "os": "Windows 11 IoT Enterprise LTSC 2024",
            "twincat": f"TC3_Line{l_num:02d}_PalletLoop.tsproj (Port 851)",
            "mes": "Siemens Opcenter Connector v4.2",
            "model": "SIMATIC IPC477E Industrial"
        })
        for target_op in [f"L{l_num:02d}-OP010", f"L{l_num:02d}-OP020", f"L{l_num:02d}-OP050"]:
            pc_station_links.append((ipc_1n, target_op, "ConveyorMaster"))

        # C. m-n Topology: 2 Coordinated Robot IPCs spanning OP080 and OP090
        ipc_mn1 = f"IPC-L{l_num:02d}-ROB-ALPHA"
        ipc_mn2 = f"IPC-L{l_num:02d}-ROB-BETA"
        for hname in [ipc_mn1, ipc_mn2]:
            pcs.append({
                "hostname": hname,
                "display": f"{hname} (Distributed m:n Cell)",
                "station": f"L{l_num:02d}-OP080",
                "org": org_id,
                "mfr": "KUKA",
                "os": "Windows 10 IoT Enterprise",
                "twincat": f"TC3_Line{l_num:02d}_RoboticsShared.tsproj (Port 851)",
                "mes": "KUKA KRC4 Edge Telemetry Connector",
                "model": "Advantech UNO-2484G Fanless Edge"
            })
            pc_station_links.append((hname, f"L{l_num:02d}-OP080", "RobotMotion"))
            pc_station_links.append((hname, f"L{l_num:02d}-OP090", "RobotMotion"))

        # D. n-1 Topology: 3 Specialized IPCs controlling single Station OP060 (Welder/Press)
        ipc_n1_plc = f"IPC-L{l_num:02d}-OP060-PLC"
        ipc_n1_vis = f"IPC-L{l_num:02d}-OP060-VISION"
        ipc_n1_mes = f"IPC-L{l_num:02d}-OP060-MES-GATE"
        pcs.append({
            "hostname": ipc_n1_plc,
            "display": f"{ipc_n1_plc} (Motion & Safety PLC)",
            "station": f"L{l_num:02d}-OP060",
            "org": org_id,
            "mfr": "Beckhoff",
            "os": "Windows 11 IoT Enterprise",
            "twincat": f"TC3_Line{l_num:02d}_LaserMotion.tsproj (Port 851)",
            "mes": "Beckhoff ADS Telemetry Streamer",
            "model": "Embedded PC C6032 High-Perf"
        })
        pcs.append({
            "hostname": ipc_n1_vis,
            "display": f"{ipc_n1_vis} (Real-time Vision Inspector)",
            "station": f"L{l_num:02d}-OP060",
            "org": org_id,
            "mfr": "Cognex",
            "os": "Windows 10 IoT Enterprise",
            "twincat": "Vision GigE Cam Driver active",
            "mes": "Cognex In-Sight Explorer 6.5.0 Connector",
            "model": "Dell OptiPlex 7090 Micro Edge"
        })
        pcs.append({
            "hostname": ipc_n1_mes,
            "display": f"{ipc_n1_mes} (Plant MES Integration Gateway)",
            "station": f"L{l_num:02d}-OP060",
            "org": org_id,
            "mfr": "Advantech",
            "os": "Debian 12 Bookworm Industrial",
            "twincat": "MQTT / OPC UA Gateway active",
            "mes": "Audi Corporate Plant MES Gateway v3",
            "model": "Advantech UNO-2271G Edge"
        })
        for hname in [ipc_n1_plc, ipc_n1_vis, ipc_n1_mes]:
            pc_station_links.append((hname, f"L{l_num:02d}-OP060", "MultiSpecialized"))

    # Convert pcs list into CSV rows
    for i, pc in enumerate(pcs, start=1):
        hname = pc["hostname"]
        meta = {
            "IPAddress": f"192.168.{10 + (i // 250)}.{(i % 250) + 1}",
            "OperatingSystem": pc["os"],
            "Model": pc["model"],
            "TwinCATProject": pc["twincat"],
            "MESClient": pc["mes"],
            "BeckhoffRtDriver": "TcRTEthernet active (Port 851 bound)",
            "Disks": {
                "C:": {"Caption": "C: (OS)", "TotalFreeGB": 142.5, "SizeGB": 256.0},
                "D:": {"Caption": "D: (Telemetry Spool)", "TotalFreeGB": 680.0, "SizeGB": 1000.0}
            },
            "RAM": "32 GB DDR4-3200 Industrial ECC",
            "CPU": "Intel Core i7-1185GRE @ 2.80GHz (4 Cores / 8 Threads)"
        }

        matching_st = next((s for s in stations if s["Name"] == pc["station"]), None)
        handle = matching_st["PinnedObjectHandle"] if matching_st else f"H-PC-{i:03d}"

        rows.append({
            "Type": "ClientPc",
            "Name": hname,
            "DisplayName": pc["display"],
            "ResponsibleTeam": "Controls Engineering (Synthetic AI Guild);Platform Operations (Synthetic AI Guild)",
            "Manufacturer": pc["mfr"],
            "Supplier": "Direct Automation Europe",
            "SerialNumber": f"SN-SYNTH-PC-{i:04d}",
            "ParentName": "",
            "StationIdentifier": pc["station"],
            "ClientPcHostname": hname,
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": handle,
            "OrganizationId": pc["org"],
            "StorageLocation": f"Control Cabinet {pc['station']}",
            "EquipmentStatus": "InMachine",
            "MachineId": "",
            "StockQuantity": "",
            "MinStockThreshold": "",
            "IsStockItem": "False",
            "Technology": "Controls & Automation",
            "MachineType": "",
            "GroupId": ""
        })

    # 3. Generate In-Machine Components (2 per station = 200 components)
    comp_templates = [
        ("PLC-S7-1516F", "Siemens S7-1516F Safety PLC", "HardwareComponent", "Siemens", "Assembly", json.dumps({"Model": "1516F-3 PN/DP", "Safety": "SIL3", "Rack": 1})),
        ("CAM-Cognex-9912", "Cognex In-Sight 9912 12MP Camera", "HardwareComponent", "Cognex", "Test", json.dumps({"Resolution": "12MP", "Lens": "25mm", "Illumination": "Polarized Red"})),
        ("DRV-AX5118", "Beckhoff AX5118 Servo Drive 18A", "HardwareComponent", "Beckhoff", "Robotics", json.dumps({"Current": "18A", "Feedback": "OCT One Cable", "Channels": 1})),
        ("ROB-KUKA-KR210", "KUKA KR210 R2700 Prime Robot Arm", "HardwareComponent", "KUKA", "Robotics", json.dumps({"Payload": "210kg", "Reach": "2700mm", "Controller": "KRC4"})),
        ("VAL-Festo-VTUG", "Festo VTUG Multi-Valve Terminal 16X", "HardwareComponent", "Festo", "Assembly", json.dumps({"Valves": 16, "Bus": "Profinet", "AirPressure": "6.5 Bar"})),
        ("SWI-Hirschmann-BOBCAT", "Hirschmann BOBCAT Managed Switch", "HardwareComponent", "Hirschmann", "Platform Operations", json.dumps({"Ports": 12, "PoE": True, "Speed": "2.5Gbps"})),
        ("SW-TwinCAT3-RT", "Beckhoff TwinCAT 3 PLC Runtime License", "SoftwareComponent", "Beckhoff", "Controls & Automation", json.dumps({"LicenseKey": "TC3-SYNTH-ENTERPRISE-100", "Version": "3.1.4026.10"})),
        ("SW-Opcenter-MES", "Siemens Opcenter MES Edge Connector", "SoftwareComponent", "Siemens", "Platform Operations", json.dumps({"LicenseKey": "OPC-MES-SYNTH-V4", "Version": "4.2.0"}))
    ]

    for i, st in enumerate(stations, start=1):
        st_name = st["Name"]
        org = st["OrganizationId"]
        for j in range(2):
            tmpl_name, tmpl_disp, tmpl_type, tmpl_mfr, tmpl_tech, tmpl_meta = comp_templates[(i + j) % len(comp_templates)]
            comp_name = f"COMP-{tmpl_name}-{st_name}-{j+1}"
            comp_disp = f"{tmpl_disp} ({st_name})"

            rows.append({
                "Type": tmpl_type,
                "Name": comp_name,
                "DisplayName": comp_disp,
                "ResponsibleTeam": st["ResponsibleTeam"],
                "Manufacturer": tmpl_mfr,
                "Supplier": "Direct Automation Europe",
                "SerialNumber": f"SN-SYNTH-CMP-{i:03d}-{j+1}",
                "ParentName": st_name,
                "StationIdentifier": st_name,
                "ClientPcHostname": "",
                "Metadata": tmpl_meta,
                "PinnedObjectHandle": "",
                "OrganizationId": org,
                "StorageLocation": f"{st_name} Mount Bay {j+1}",
                "EquipmentStatus": "InMachine",
                "MachineId": st_name,
                "StockQuantity": "",
                "MinStockThreshold": "",
                "IsStockItem": "False",
                "Technology": tmpl_tech,
                "MachineType": "",
                "GroupId": ""
            })

    # 4. Generate Exactly 550 Serialized Spare Parts in Stock (500 InStorage + 50 UnderRepair)
    spare_templates = [
        ("SIEM-S7-1500", "Siemens S7-1500 CPU 1516-3 PN/DP (Spare)", "Siemens", "Assembly", 2850000),
        ("BECK-AX5000", "Beckhoff AX5000 Servo Drive 12A (Spare)", "Beckhoff", "Robotics", 1450000),
        ("COGN-IN9912", "Cognex In-Sight 9912 12MP Inspection Camera", "Cognex", "Test", 2200000),
        ("KUKA-KRC4-PC", "KUKA KRC4 Industrial Robot Controller PC", "KUKA", "Robotics", 3900000),
        ("FEST-VTUG-16", "Festo VTUG Valve Terminal 16-Valve Multi", "Festo", "Assembly", 850000),
        ("ATLAS-STR-61", "Atlas Copco Tensor STR-61 Tightening Spindle", "Atlas Copco", "Fastening", 3100000),
        ("SICK-MICROSCAN", "SICK microScan3 Core Safety Laser Scanner", "SICK", "Assembly", 1250000),
        ("SEW-MOVIPRO", "SEW Eurodrive MOVIPRO Decentralized Inverter", "SEW Eurodrive", "Assembly", 1600000),
        ("BALL-RFID-RWD", "Balluff BIS-M 13.56MHz High-Temp RFID Reader", "Balluff", "Test", 480000),
        ("TRUMPF-OPTIC", "Trumpf BEO D70 Laser Seam Optical Focus Head", "Trumpf", "Welding", 4500000),
        ("TOX-E-PRESS", "TOX ElectricDrive 50kN Servo Press Drive", "TOX Pressotechnik", "Fastening", 4800000),
        ("HIRS-BOBCAT", "Hirschmann BRS20 Industrial Managed Switch", "Hirschmann", "Platform Operations", 720000)
    ]

    for k in range(1, 551):
        tmpl_code, tmpl_title, mfr, tech, cost = spare_templates[(k - 1) % len(spare_templates)]
        is_repair = (k <= 50)
        status = "UnderRepair" if is_repair else "InStorage"
        aisle = chr(65 + ((k - 1) % 7))
        shelf = f"Repair Depot Bay {((k % 4) + 1)}" if is_repair else f"Warehouse Aisle {aisle} – Shelf {((k % 8) + 1)}-{((k % 5) + 1)}"
        sn = f"SN-FAKE-{mfr[:4].upper()}-{k:04d}"

        meta = {
            "CostInHUF": cost,
            "Status": status,
            "WarehouseLocation": shelf,
            "InspectionDate": "2026-09-01T00:00:00Z",
            "CalibrationValidUntil": "2027-09-01T00:00:00Z",
            "AssetClass": "High-Value Serialized Spare"
        }

        rows.append({
            "Type": "HardwareComponent",
            "Name": f"SPARE-{tmpl_code}-{k:04d}",
            "DisplayName": f"Synthetic {tmpl_title} #{k:03d}",
            "ResponsibleTeam": "Plant Maintenance (Synthetic AI Guild)",
            "Manufacturer": mfr,
            "Supplier": "Direct Automation Europe",
            "SerialNumber": sn,
            "ParentName": "",
            "StationIdentifier": "",
            "ClientPcHostname": "",
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": "",
            "OrganizationId": "org-maintenance",
            "StorageLocation": shelf,
            "EquipmentStatus": status,
            "MachineId": "",
            "StockQuantity": "",
            "MinStockThreshold": "",
            "IsStockItem": "False",
            "Technology": tech,
            "MachineType": "",
            "GroupId": ""
        })

    # 5. Generate Bulk Consumables (Pneumatics, Fasteners, Strut Profiles, Wiring)
    bulk_items = [
        # Pneumatics
        ("STK-PNEU-QS4", "Simulated Festo QS-1/8-4 Quick Push-In Fittings", "Festo", "Assembly", 180, 40, "Bin PNEU-01"),
        ("STK-PNEU-QS6", "Simulated Festo QS-1/4-6 Quick Couplers", "Festo", "Assembly", 240, 50, "Bin PNEU-02"),
        ("STK-PNEU-QS8", "Simulated Festo QS-1/4-8 Pneumatic Fittings", "Festo", "Assembly", 310, 60, "Bin PNEU-03"),
        ("STK-PNEU-QS10", "Simulated Festo QS-3/8-10 Heavy Pneumatic Couplers", "Festo", "Assembly", 140, 30, "Bin PNEU-04"),
        ("STK-TUBE-PU8-BL", "Simulated Festo PUN-H-8x1.25 Blue Tubing (50m Roll)", "Festo", "Assembly", 25, 5, "Bin PNEU-05"),
        ("STK-TUBE-PU6-BK", "Simulated Festo PUN-H-6x1.0 Black Tubing (50m Roll)", "Festo", "Assembly", 30, 8, "Bin PNEU-06"),
        # Fasteners
        ("STK-SCRW-M3-10", "Simulated Würth DIN 912 M3x10mm Socket Head Screws (100x Box)", "Würth", "Fastening", 85, 20, "Bin FAST-01"),
        ("STK-SCRW-M4-16", "Simulated Würth DIN 912 M4x16mm Socket Head Screws (100x Box)", "Würth", "Fastening", 120, 25, "Bin FAST-02"),
        ("STK-SCRW-M5-20", "Simulated Würth DIN 912 M5x20mm High-Tensile Bolts (100x Box)", "Würth", "Fastening", 95, 20, "Bin FAST-03"),
        ("STK-SCRW-M6-25", "Simulated Würth DIN 912 M6x25mm Grade 8.8 Screws (100x Box)", "Würth", "Fastening", 150, 30, "Bin FAST-04"),
        ("STK-SCRW-M8-30", "Simulated Würth DIN 912 M8x30mm Grade 10.9 Bolts (50x Box)", "Würth", "Fastening", 80, 20, "Bin FAST-05"),
        ("STK-NUT-M8-FLG", "Simulated Würth DIN 6923 M8 Hex Flange Locking Nuts (100x Box)", "Würth", "Fastening", 110, 25, "Bin FAST-06"),
        ("STK-BOLT-M10-40", "Simulated Würth DIN 7991 M10x40mm Countersunk Hex Bolts (50x)", "Würth", "Fastening", 60, 15, "Bin FAST-07"),
        # Aluminum Structural Profiles
        ("STK-ALUM-4040-1M", "Simulated Bosch Rexroth 40x40 Strut Profile (1000mm Cut)", "Bosch Rexroth", "Assembly", 45, 10, "Rack STRUT-A1"),
        ("STK-ALUM-4040-2M", "Simulated Bosch Rexroth 40x40 Strut Profile (2000mm Bar)", "Bosch Rexroth", "Assembly", 35, 8, "Rack STRUT-A2"),
        ("STK-ALUM-4545-1M", "Simulated Bosch Rexroth 45x45 Heavy Modular Strut (1000mm)", "Bosch Rexroth", "Assembly", 50, 12, "Rack STRUT-B1"),
        ("STK-ALUM-4590-2M", "Simulated Bosch Rexroth 45x90 Structural Beam (2000mm Bar)", "Bosch Rexroth", "Assembly", 20, 5, "Rack STRUT-B2"),
        ("STK-ALUM-BRKT-40", "Simulated Bosch Rexroth 40x40 Cast Aluminum Corner Brackets", "Bosch Rexroth", "Assembly", 250, 50, "Bin STRUT-C1"),
        ("STK-ALUM-TNUT-M8", "Simulated Bosch Rexroth M8 T-Slot Roll-In Spring Nuts (100x)", "Bosch Rexroth", "Assembly", 180, 40, "Bin STRUT-C2"),
        # Electrical & Wiring
        ("STK-TERM-PT25", "Simulated Phoenix Contact PT 2.5 DIN Rail Terminal Blocks", "Phoenix Contact", "Assembly", 350, 80, "Bin ELEC-01"),
        ("STK-FERR-15MM", "Simulated Phoenix Contact Insulated Wire Ferrules 1.5mm² (500x)", "Phoenix Contact", "Assembly", 90, 20, "Bin ELEC-02"),
        ("STK-TIE-200MM", "Simulated UV-Resistant Industrial Zip Cable Ties 200mm (500x)", "HellermannTyton", "Assembly", 140, 30, "Bin ELEC-03"),
        ("STK-DISP-NZ25", "Simulated Nordson Precision Dispenser Nozzles 0.25mm Luer Lock", "Nordson", "Dispensing", 65, 15, "Bin DISP-01")
    ]

    for idx, (b_name, b_disp, b_mfr, b_tech, b_qty, b_min, b_loc) in enumerate(bulk_items, start=1):
        meta = {
            "StockQuantity": b_qty,
            "MinStockThreshold": b_min,
            "StorageBin": b_loc,
            "LotNumber": f"LOT-2026-AI-{idx:03d}",
            "UnitOfMeasure": "Units / Packs"
        }
        rows.append({
            "Type": "HardwareComponent",
            "Name": b_name,
            "DisplayName": b_disp,
            "ResponsibleTeam": "Plant Maintenance (Synthetic AI Guild)",
            "Manufacturer": b_mfr,
            "Supplier": "RS Components",
            "SerialNumber": f"LOT-2026-AI-{idx:03d}",
            "ParentName": "",
            "StationIdentifier": "",
            "ClientPcHostname": "",
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": "",
            "OrganizationId": "org-maintenance",
            "StorageLocation": b_loc,
            "EquipmentStatus": "InStorage",
            "MachineId": "",
            "StockQuantity": str(b_qty),
            "MinStockThreshold": str(b_min),
            "IsStockItem": "True",
            "Technology": b_tech,
            "MachineType": "",
            "GroupId": ""
        })

    fieldnames = [
        "Type", "Name", "DisplayName", "ResponsibleTeam", "Manufacturer", "Supplier",
        "SerialNumber", "ParentName", "StationIdentifier", "ClientPcHostname", "Metadata",
        "PinnedObjectHandle", "OrganizationId", "StorageLocation", "EquipmentStatus",
        "MachineId", "StockQuantity", "MinStockThreshold", "IsStockItem", "Technology",
        "MachineType", "GroupId"
    ]
    with open(output_path, mode="w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"✓ Generated {len(rows)} enterprise inventory entries in {output_path}")
    print(f"  - 100 Diverse Machines across 8 Lines")
    print(f"  - {len(pcs)} Edge IPC Controllers (1-1, 1-n, m-n, n-1)")
    print(f"  - 200 In-Machine Components")
    print(f"  - 550 Serialized Spare Parts in Stock (500 InStorage, 50 UnderRepair)")
    print(f"  - {len(bulk_items)} Bulk Consumables (Pneumatics, Fasteners, Strut Profiles)")
    return rows, pc_station_links

def generate_sql(csv_path=CSV_FILE, output_path=SQL_FILE, pc_station_links=None):
    print("Generating transactional PostgreSQL seed script with FULL TABLE PURGE / NUKE...")
    with open(csv_path, mode='r', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    sql = [
        "-- Heimdall Enterprise Plant Seed SQL (100 Machines, 8 Lines, 550 Serialized Parts, 60 Fake AI Users)",
        "-- Auto-generated by seed_pipeline.py with COMPLETE TABLE PURGE",
        "SET statement_timeout = 0;",
        "BEGIN;",
        "SET search_path TO backend, auth, public;",
        "",
        "-- Ensure Schemas exist",
        "CREATE SCHEMA IF NOT EXISTS backend;",
        "CREATE SCHEMA IF NOT EXISTS auth;",
        "",
        "-- PURGE / NUKE ALL PREVIOUS DATA",
        "TRUNCATE TABLE backend.inventory_items CASCADE;",
        "TRUNCATE TABLE backend.client_pcs CASCADE;",
        "TRUNCATE TABLE backend.stations CASCADE;",
        "TRUNCATE TABLE backend.manufacturers CASCADE;",
        "TRUNCATE TABLE backend.suppliers CASCADE;",
        "TRUNCATE TABLE backend.responsible_teams CASCADE;",
        "TRUNCATE TABLE backend.\"ItemResponsibilities\" CASCADE;",
        "TRUNCATE TABLE backend.\"PcResponsibilities\" CASCADE;",
        "TRUNCATE TABLE backend.\"StationControllers\" CASCADE;",
        "TRUNCATE TABLE backend.equipment_interconnects CASCADE;",
        "TRUNCATE TABLE backend.maintenance_tickets CASCADE;",
        "TRUNCATE TABLE backend.ticket_comments CASCADE;",
        "TRUNCATE TABLE backend.security_group_mappings CASCADE;",
        "TRUNCATE TABLE backend.ad_ou_governances CASCADE;",
        "TRUNCATE TABLE backend.system_settings CASCADE;",
        "TRUNCATE TABLE backend.client_certificates CASCADE;",
        "TRUNCATE TABLE backend.schema_version_manifest CASCADE;",
        "TRUNCATE TABLE backend.audit_logs CASCADE;",
        "TRUNCATE TABLE backend.malformed_telemetry_quarantine CASCADE;",
        "",
        "-- Wipe Better-Auth legacy membership tables",
        "TRUNCATE TABLE auth.member CASCADE;",
        "TRUNCATE TABLE auth.organization CASCADE;",
        "TRUNCATE TABLE auth.session CASCADE;",
        "TRUNCATE TABLE auth.account CASCADE;",
        "TRUNCATE TABLE auth.user CASCADE;",
        "",
        "-- Seed 16 Organizations into auth.organization",
    ]

    for org in ORGANIZATIONS:
        org_id = org["id"]
        org_name = org["name"].replace("'", "''")
        org_slug = org["slug"].replace("'", "''")
        sql.append(f"INSERT INTO auth.organization (id, name, slug, created_at) VALUES ('{org_id}', '{org_name}', '{org_slug}', NOW()) ON CONFLICT (id) DO NOTHING;")

    sql.append("\n-- Seed 60 Obviously Fake, AI-Generated Users into auth.user and auth.member")
    for uid, uname, uemail, urole, utitle, udept, uorgs in FAKE_USERS:
        u_name_esc = uname.replace("'", "''")
        sql.append(f"INSERT INTO auth.user (id, name, email, email_verified, role, created_at, updated_at) "
                   f"VALUES ('{uid}', '{u_name_esc}', '{uemail}', true, '{urole}', NOW(), NOW()) ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, email = EXCLUDED.email;")
        for org_id in uorgs:
            mem_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{uid}:{org_id}"))
            mem_role = "owner" if urole == "plant_director" else "admin" if urole in ("plant_engineering_manager", "lead_engineer", "shift_leader") else "member"
            sql.append(f"INSERT INTO auth.member (id, organization_id, user_id, role, created_at) VALUES ('{mem_id}', '{org_id}', '{uid}', '{mem_role}', NOW()) ON CONFLICT (id) DO NOTHING;")

    # Seed Manufacturers, Suppliers, Responsible Teams
    manufacturers = set(r['Manufacturer'] for r in rows if r.get('Manufacturer'))
    suppliers = set(r['Supplier'] for r in rows if r.get('Supplier'))
    teams = set()
    for r in rows:
        if r.get('ResponsibleTeam'):
            for t in r['ResponsibleTeam'].split(';'):
                teams.add(t.strip())

    sql.append("\n-- Seed Reference Tables (Manufacturers, Suppliers, Teams)")
    for m in sorted(manufacturers):
        m_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, m))
        m_esc = m.replace("'", "''")
        sql.append(f"INSERT INTO backend.manufacturers (id, name) VALUES ('{m_id}', '{m_esc}') ON CONFLICT (name) DO NOTHING;")
    for s in sorted(suppliers):
        s_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, s))
        s_esc = s.replace("'", "''")
        sql.append(f"INSERT INTO backend.suppliers (id, name) VALUES ('{s_id}', '{s_esc}') ON CONFLICT (name) DO NOTHING;")
    team_ids = {}
    for t in sorted(teams):
        t_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, t))
        team_ids[t] = t_id
        t_esc = t.replace("'", "''")
        sql.append(f"INSERT INTO backend.responsible_teams (id, name) VALUES ('{t_id}', '{t_esc}') ON CONFLICT (name) DO NOTHING;")

    # Seed Client PCs
    sql.append("\n-- Seed Client PCs (Edge IPCs with TwinCAT & MES)")
    item_ids = {row['Name']: str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name'])) for row in rows if row['Type'] != 'ClientPc'}
    pc_ids = {row['Name']: str(uuid.uuid5(uuid.NAMESPACE_DNS, row['Name'])) for row in rows if row['Type'] == 'ClientPc'}

    for row in rows:
        if row['Type'] == 'ClientPc':
            item_id = pc_ids[row['Name']]
            pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
            org_id = f"'{row['OrganizationId']}'" if row.get('OrganizationId') else "'org-platform'"
            mac = f"02:{item_id[0:2]}:{item_id[2:4]}:{item_id[4:6]}:{item_id[6:8]}:{item_id[9:11]}".upper()
            sql.append(f"INSERT INTO backend.client_pcs (id, name, mac_address, hostname, machine_identifier, pinned_object_handle, organization_id) "
                       f"VALUES ('{item_id}', '{row['Name']}', '{mac}', '{row['ClientPcHostname'] or row['Name']}', 'HW-{item_id[:8]}', {pin_handle}, {org_id}) "
                       f"ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle, organization_id = EXCLUDED.organization_id;")

    # Seed 100 Machines into inventory_items and stations table
    sql.append("\n-- Seed 100 Diverse Machines across 8 Automated Lines")
    for row in rows:
        if row['Type'] == 'Machine':
            item_id = item_ids[row['Name']]
            pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
            org_id = f"'{row['OrganizationId']}'" if row.get('OrganizationId') else "'org-line-01'"
            m_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Manufacturer'])}'" if row.get('Manufacturer') else "NULL"
            s_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Supplier'])}'" if row.get('Supplier') else "NULL"
            metadata = (row.get('Metadata') or "{}").replace("'", "''")
            display_name = row.get('DisplayName', '').replace("'", "''")
            name = row['Name'].replace("'", "''")
            loc_val = row.get('StorageLocation', '').replace("'", "''") if row.get('StorageLocation') else None
            storage_loc = f"'{loc_val}'" if loc_val else "NULL"
            tech_val = row.get('Technology', '').replace("'", "''") if row.get('Technology') else None
            tech = f"'{tech_val}'" if tech_val else "NULL"

            sql.append(f"INSERT INTO backend.inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id, storage_location, equipment_status, is_stock_item, stock_quantity, min_stock_threshold, technology, machine_id) "
                       f"VALUES ('{item_id}', '{name}', '{display_name}', {m_id}, {s_id}, '{metadata}'::jsonb, '{row.get('SerialNumber', '')}', {org_id}, {storage_loc}, 'InMachine', false, NULL, NULL, {tech}, NULL) "
                       f"ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, metadata = EXCLUDED.metadata, organization_id = EXCLUDED.organization_id, storage_location = EXCLUDED.storage_location, equipment_status = EXCLUDED.equipment_status, technology = EXCLUDED.technology;")

            mt = row.get('MachineType', '').replace("'", "''") if row.get('MachineType') else None
            m_type = f"'{mt}'" if mt else "NULL"
            gid = row.get('GroupId', '').replace("'", "''") if row.get('GroupId') else None
            grp_id = f"'{gid}'" if gid else "NULL"
            sql.append(f"INSERT INTO backend.stations (id, custom_identifier, pinned_object_handle, machine_type, group_id) "
                       f"VALUES ('{item_id}', '{row['StationIdentifier'] or row['Name']}', {pin_handle}, {m_type}, {grp_id}) "
                       f"ON CONFLICT (id) DO UPDATE SET pinned_object_handle = EXCLUDED.pinned_object_handle, machine_type = EXCLUDED.machine_type, group_id = EXCLUDED.group_id;")

            if row.get('ResponsibleTeam'):
                for t in row['ResponsibleTeam'].split(';'):
                    t = t.strip()
                    if t in team_ids:
                        sql.append(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ('{item_id}', '{team_ids[t]}') ON CONFLICT DO NOTHING;")

    # Seed In-Machine Components, 550 Serialized Spare Parts, and Bulk Stock
    sql.append("\n-- Seed In-Machine Components, 550 Serialized Spare Parts, and Bulk Stock")
    for row in rows:
        if row['Type'] not in ('ClientPc', 'Machine'):
            item_id = item_ids[row['Name']]
            pin_handle = f"'{row['PinnedObjectHandle']}'" if row.get('PinnedObjectHandle') else "NULL"
            org_id = f"'{row['OrganizationId']}'" if row.get('OrganizationId') else "'org-maintenance'"
            m_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Manufacturer'])}'" if row.get('Manufacturer') else "NULL"
            s_id = f"'{uuid.uuid5(uuid.NAMESPACE_DNS, row['Supplier'])}'" if row.get('Supplier') else "NULL"
            metadata = (row.get('Metadata') or "{}").replace("'", "''")
            display_name = row.get('DisplayName', '').replace("'", "''")
            name = row['Name'].replace("'", "''")
            is_stock = "true" if str(row.get('IsStockItem', '')).lower() == 'true' else "false"
            eq_status = f"'{row.get('EquipmentStatus') or 'InStorage'}'"
            loc_val = row.get('StorageLocation', '').replace("'", "''") if row.get('StorageLocation') else None
            storage_loc = f"'{loc_val}'" if loc_val else "NULL"
            stock_qty = str(row['StockQuantity']) if row.get('StockQuantity') not in (None, '', 'None') else "NULL"
            min_thresh = str(row['MinStockThreshold']) if row.get('MinStockThreshold') not in (None, '', 'None') else "NULL"
            tech_val = row.get('Technology', '').replace("'", "''") if row.get('Technology') else None
            tech = f"'{tech_val}'" if tech_val else "NULL"
            machine_id = "NULL"
            if row.get('StationIdentifier') and row['StationIdentifier'] in item_ids:
                machine_id = f"'{item_ids[row['StationIdentifier']]}'"

            sql.append(f"INSERT INTO backend.inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id, storage_location, equipment_status, is_stock_item, stock_quantity, min_stock_threshold, technology, machine_id) "
                       f"VALUES ('{item_id}', '{name}', '{display_name}', {m_id}, {s_id}, '{metadata}'::jsonb, '{row.get('SerialNumber', '')}', {org_id}, {storage_loc}, {eq_status}, {is_stock}, {stock_qty}, {min_thresh}, {tech}, {machine_id}) "
                       f"ON CONFLICT (id) DO UPDATE SET display_name = EXCLUDED.display_name, metadata = EXCLUDED.metadata, organization_id = EXCLUDED.organization_id, storage_location = EXCLUDED.storage_location, equipment_status = EXCLUDED.equipment_status, is_stock_item = EXCLUDED.is_stock_item, stock_quantity = EXCLUDED.stock_quantity, min_stock_threshold = EXCLUDED.min_stock_threshold, technology = EXCLUDED.technology, machine_id = EXCLUDED.machine_id;")

            if row['Type'] == 'HardwareComponent':
                sql.append(f"INSERT INTO backend.hardware_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")
            elif row['Type'] == 'SoftwareComponent':
                sql.append(f"INSERT INTO backend.software_assets (id) VALUES ('{item_id}') ON CONFLICT (id) DO NOTHING;")

            if row.get('ResponsibleTeam'):
                for t in row['ResponsibleTeam'].split(';'):
                    t = t.strip()
                    if t in team_ids:
                        sql.append(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ('{item_id}', '{team_ids[t]}') ON CONFLICT DO NOTHING;")

    # Seed Station Controllers (1-1, 1-n, m-n, n-1)
    sql.append("\n-- Seed Station Controller Edges (1-1, 1-n, m-n, n-1 Topologies)")
    if pc_station_links:
        for pc_name, st_name, role in pc_station_links:
            if pc_name in pc_ids and st_name in item_ids:
                pc_id = pc_ids[pc_name]
                st_id = item_ids[st_name]
                sc_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{pc_id}:{st_id}"))
                sql.append(f"INSERT INTO backend.\"StationControllers\" (id, client_pc_id, machine_id) VALUES ('{sc_id}', '{pc_id}', '{st_id}') ON CONFLICT DO NOTHING;")

    # Seed Security Group Mappings
    sql.append("\n-- Seed Security Group Mappings for 8 Lines & Tech Guilds")
    sec_group_defs = [
        ("EntraID", "sg-entra-admins", "OT Plant Administrators", "system_admin", "org-platform"),
        ("ActiveDirectory", "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp", "Line 01 Controls Engineers", "controls_engineer", "org-line-01"),
        ("ActiveDirectory", "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp", "Line 02 Assembly Techs", "technician", "org-line-02"),
        ("ActiveDirectory", "CN=SG-Line03-Powertrain,OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp", "Line 03 Powertrain Engineers", "engineer", "org-line-03"),
        ("ActiveDirectory", "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp", "Line 04 SMT Placement Leads", "lead_engineer", "org-line-04"),
        ("ActiveDirectory", "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp", "Line 05 Robotic Welders", "engineer", "org-line-05"),
        ("ActiveDirectory", "CN=SG-Line06-PressFit,OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp", "Line 06 Press Fit Techs", "technician", "org-line-06"),
        ("ActiveDirectory", "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp", "Line 07 Quality Metrologists", "lead_engineer", "org-line-07"),
        ("ActiveDirectory", "CN=SG-Line08-EOL,OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp", "Line 08 EOL Vehicle Techs", "technician", "org-line-08"),
        ("ActiveDirectory", "CN=SG-Plant-Maintenance,OU=Maintenance,OU=EngineeringDisciplines,DC=factory,DC=corp", "Plant Maintenance Specialists", "technician", "org-maintenance")
    ]
    for idp, gid, dname, role, org in sec_group_defs:
        m_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"{idp}:{gid}"))
        sql.append(f"INSERT INTO backend.security_group_mappings (id, identity_provider, group_identifier, display_name, mapped_role, organization_id, is_enabled, created_at, updated_at) "
                   f"VALUES ('{m_id}', '{idp}', '{gid}', '{dname}', '{role}', '{org}', true, NOW(), NOW()) ON CONFLICT (id) DO NOTHING;")

    # Seed AD OU Governance
    sql.append("\n-- Seed Active Directory OUs Governance")
    ou_defs = [
        ("OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal"),
        ("OU=ControlsEngineering,DC=factory,DC=corp", "read_write", True, "dr.algorithmus.prime.ai@fake-factory.internal")
    ]
    for ou, alevel, approved, approver in ou_defs:
        ou_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"ou:{ou}"))
        sql.append(f"INSERT INTO backend.ad_ou_governances (id, ou_path, access_level, is_approved, approved_by, approved_at, notes, created_at, updated_at) "
                   f"VALUES ('{ou_id}', '{ou}', '{alevel}', true, '{approver}', NOW(), 'Approved production corridor', NOW(), NOW()) ON CONFLICT (id) DO NOTHING;")

    sql.append("\nCOMMIT;")

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(sql))
    print(f"✓ Generated {output_path} with complete PURGE / NUKE and re-seed logic.")

def generate_topology(output_path=TOPOLOGY_FILE, stations=None, pcs=None, links=None):
    print("Generating production_topology.json hierarchy for 8 lines and 100 stations...")
    lines_dict = {}
    for l_cfg in LINE_CONFIGS:
        lines_dict[l_cfg["org_id"]] = {
            "id": l_cfg["org_id"],
            "line_num": l_cfg["line_num"],
            "name": l_cfg["name"],
            "tech": l_cfg["tech"],
            "stations": []
        }

    st_lookup = {s["Name"]: s for s in (stations or [])}
    pc_lookup = {p["Name"]: p for p in (pcs or [])}

    # Group stations into their lines
    for st in (stations or []):
        org_id = st["OrganizationId"]
        if org_id in lines_dict:
            st_name = st["Name"]
            # Find linked IPCs from pc_station_links
            linked_pc_names = [l[0] for l in (links or []) if l[1] == st_name]
            st_pcs = []
            for p_name in linked_pc_names:
                p_data = pc_lookup.get(p_name)
                if p_data:
                    meta = json.loads(p_data["Metadata"]) if p_data.get("Metadata") else {}
                    st_pcs.append({
                        "id": p_name,
                        "hostname": p_name,
                        "ip": meta.get("IPAddress", "192.168.1.1"),
                        "os": meta.get("OperatingSystem", "Windows 10 IoT"),
                        "status": "Healthy",
                        "twincat": meta.get("TwinCATProject", "TC3 (Port 851)"),
                        "mes": meta.get("MESClient", "MES Connector v3")
                    })

            lines_dict[org_id]["stations"].append({
                "id": st_name,
                "name": st["DisplayName"],
                "type": st["MachineType"],
                "technology": st["Technology"],
                "team": st["ResponsibleTeam"],
                "pinnedObjectHandle": st["PinnedObjectHandle"],
                "pcs": st_pcs
            })

    topology = {
        "production_hall": {
            "name": "Heimdall Smart Factory Giga-01 (Synthetic AI Facility)",
            "lines": list(lines_dict.values())
        }
    }

    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(topology, f, indent=2)
    print(f"✓ Generated {output_path} with 8 automated lines and 100 stations.")

def validate():
    print("=== Validating Seed Data Referential Integrity ===")
    if not os.path.exists(CSV_FILE) or not os.path.exists(SQL_FILE) or not os.path.exists(TOPOLOGY_FILE):
        print("FAIL: Missing generated files.")
        return False

    with open(CSV_FILE, mode='r', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    stations = [r for r in rows if r['Type'] == 'Machine']
    pcs = [r for r in rows if r['Type'] == 'ClientPc']
    spares = [r for r in rows if r['Type'] not in ('Machine', 'ClientPc') and str(r.get('IsStockItem', '')).lower() != 'true' and 'SPARE' in r['Name']]
    bulk = [r for r in rows if str(r.get('IsStockItem', '')).lower() == 'true']

    print(f"✓ Inventory CSV rows: {len(rows)}")
    print(f"  - Machines: {len(stations)} (Target: 100)")
    print(f"  - Client PCs: {len(pcs)}")
    print(f"  - Serialized Spare Items in Stock: {len(spares)} (Target: 550)")
    print(f"  - Bulk Consumables: {len(bulk)} (Target: >= 20)")

    assert len(stations) == 100, f"Expected 100 stations, got {len(stations)}"
    assert len(spares) == 550, f"Expected 550 serialized spare items, got {len(spares)}"
    assert len(bulk) >= 20, f"Expected >= 20 bulk consumables, got {len(bulk)}"

    # Check DXF handles
    handles = set(r['PinnedObjectHandle'] for r in stations if r['PinnedObjectHandle'])
    assert len(handles) == 100, f"Expected 100 unique station DXF handles, got {len(handles)}"
    print(f"✓ Station DXF handles: {len(handles)} unique handles (L01-OP010 .. L08-OP130)")

    # Validate topology file
    with open(TOPOLOGY_FILE, 'r', encoding='utf-8') as f:
        top_data = json.load(f)
    assert len(top_data["production_hall"]["lines"]) == 8, "Expected 8 production lines in topology"
    total_top_st = sum(len(l["stations"]) for l in top_data["production_hall"]["lines"])
    assert total_top_st == 100, f"Expected 100 stations in topology, got {total_top_st}"
    print(f"✓ Production topology validated: 8 lines, 100 stations")

    print("✓ All Seed Data integrity checks PASSED successfully!")
    return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Heimdall Unified Seed Data Pipeline")
    parser.add_argument("--generate-all", action="store_true", help="Generate CSV and SQL seed files")
    parser.add_argument("--validate", action="store_true", help="Validate referential integrity")
    args = parser.parse_args()

    rows, links = generate_csv()
    st_rows = [r for r in rows if r['Type'] == 'Machine']
    pc_rows = [r for r in rows if r['Type'] == 'ClientPc']
    generate_sql(CSV_FILE, SQL_FILE, links)
    generate_topology(TOPOLOGY_FILE, st_rows, pc_rows, links)
    validate()
