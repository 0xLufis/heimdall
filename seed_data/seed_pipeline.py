#!/usr/bin/env python3
"""
Heimdall Unified Seed Data Pipeline & Integrity Validator (enterprise dataset "Giga-01")

Generates the enterprise-scale REFERENCE dataset: 100 machines across 8 automated lines, 56 edge
IPCs in 4 control topologies (1-1, 1-n, m-n, n-1) so that every station has a reporting controller,
200 installed components bound to machine component slots, 550 serialized spares + bulk consumables,
the machine group hierarchy, maintenance crews, skills, dispatch rules and 107 overtly fake users.

It contains master data only. Everything that represents activity (station telemetry, IPC
heartbeats, tickets, spare-part consumption) is produced by the fleet simulator.

    python3 seed_data/seed_pipeline.py --generate-all   # regenerate CSV / topology / fixture / SQL
    python3 seed_data/seed_pipeline.py --validate       # referential integrity + staleness check
"""

import argparse
import csv
import io
import json
import os
import random
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reference_checks import (DIGITAL_TWIN_GLB, FLOOR_PLAN_DXF, REPO_ROOT, TWIN_SLOTS,  # noqa: E402
                              read_dxf_insert_handles, read_glb_node_names, read_heimdall_roles)

CSV_FILE = os.path.join(os.path.dirname(__file__), 'inventory_seed.csv')
SQL_FILE = os.path.join(os.path.dirname(__file__), 'incremental_seed.sql')
TOPOLOGY_FILE = os.path.join(os.path.dirname(__file__), 'production_topology.json')

# Per-component links written into the dev dataset. The UI has no built-in defaults for these:
# a controller's remote view and a station's 3D twin are resolved from the row itself.
# No "Host" means "the host serving the web UI" (the Windows dev container publishes noVNC on :8006).
DEV_REMOTE_ACCESS_LINK = {"Vnc": {"Port": 8006, "Path": "websockify"}}
DEV_STATION_MODEL_URL = "/sample/station_digital_twin.glb"

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
    ("usr-synth-18", "Glitchy McGlitch", "glitchy.mcglitch.ai@fake-factory.internal", "technician", "Diagnostic Test Operator", "Testing", ["org-maintenance", "org-line-08"]),
    ("usr-synth-19", "Matrix O'Gradient", "matrix.ogradient.ai@fake-factory.internal", "engineer", "Optimization Mathematical Modeler", "Plant Engineering", ["org-controls", "org-audi-proj", "org-line-03"]),
    ("usr-synth-20", "Bitty Byte-Bender", "bitty.bytebender.ai@fake-factory.internal", "technician", "Fieldbus Wiring Specialist", "Controls & Automation", ["org-controls", "org-line-03"]),
    ("usr-synth-21", "Epoch MacEpochface", "epoch.macepochface.ai@fake-factory.internal", "engineer", "Reflow Thermal Profiler", "SMT & Microchips", ["org-smt", "org-line-04"]),
    ("usr-synth-22", "Transformer D. Model", "transformer.d.model.ai@fake-factory.internal", "lead_engineer", "Multi-Agent Fleet Coordinator", "Robotics", ["org-robotics", "org-line-02", "org-line-05"]),
    ("usr-synth-23", "Cyberia Glitchcraft", "cyberia.glitchcraft.ai@fake-factory.internal", "technician", "Laser Optics Calibration Tech", "Tooling", ["org-assembly", "org-line-01", "org-line-05"]),
    ("usr-synth-24", "Robo-Copernicus", "robo.copernicus.ai@fake-factory.internal", "engineer", "Astronomical Motion Kinematicist", "Robotics", ["org-robotics", "org-audi-proj", "org-line-06"]),
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
    ("usr-synth-47", "Leakcheck Lola", "leakcheck.lola.ai@fake-factory.internal", "engineer", "Helium Sniffer Chamber Lead", "Testing", ["org-maintenance", "org-line-02"]),
    ("usr-synth-48", "Voltmeter Victor", "voltmeter.victor.ai@fake-factory.internal", "technician", "Hi-Pot Electrical Tester", "Testing", ["org-maintenance", "org-line-01", "org-line-08"]),
    ("usr-synth-49", "Dynamo Dan", "dynamo.dan.ai@fake-factory.internal", "engineer", "Roll Bench Dyno Specialist", "Testing", ["org-assembly", "org-line-08"]),
    ("usr-synth-50", "Radar Rhonda", "radar.rhonda.ai@fake-factory.internal", "engineer", "ADAS Target Array Aligner", "Testing", ["org-vision", "org-line-08"]),
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

# Master catalogue of all system, persona, technician candidate, and ticketing users
# Ensures that NO user can exist without a registered Better-Auth user profile
ADDITIONAL_SYSTEM_USERS = [
    # Demo Personas (from useAuthSession.ts)
    ("usr-sysadmin-1", "Root System Administrator", "sysadmin@heimdall.dev", "system_admin", ["org-platform"]),
    ("usr-director-1", "Dr. Henrik Weber (Plant Director)", "henrik.weber@factory.corp", "plant_director", ["org-platform", "org-audi-proj"]),
    ("usr-plant-eng-1", "Gábor Varga (Plant Engineering Manager)", "gabor.varga@factory.corp", "plant_engineering_manager", ["org-platform", "org-controls"]),
    ("usr-senior-eng-1", "Elena Rostova (Senior Engineering Manager)", "elena.rostova@factory.corp", "senior_engineering_manager", ["org-platform", "org-line-06"]),
    ("usr-heimdall-admin-1", "Heimdall Administrator", "admin.platform@heimdall.dev", "heimdall_admin", ["org-platform"]),
    ("usr-itadmin-1", "IT Infrastructure Administrator", "it.admin@heimdall.dev", "it_admin", ["org-platform"]),
    ("usr-itsiteadmin-1", "Marcus Vance (IT Site Administrator)", "marcus.vance@factory.corp", "it_site_admin", ["org-platform"]),
    ("usr-engadmin-1", "Engineering Administrator", "eng.admin@heimdall.dev", "engineering_admin", ["org-platform"]),
    ("usr-manager-andras", "András Molnár (Plant Manager)", "andras.manager@heimdall.dev", "manager", ["org-platform"]),
    ("usr-op-planner-1", "András Molnár (Operative Planner)", "andras.planner@factory.corp", "operative_planner", ["org-platform"]),
    ("usr-gl-assy-1", "Sally Vance (Group Leader – Fastening & Assembly)", "sally.vance@factory.corp", "group_leader", ["org-assembly"]),
    ("usr-ferenc", "Shift Leader Ferenc", "ferenc.leader@heimdall.dev", "shift_leader", ["org-platform"]),
    ("usr-eng-robotics-1", "Alex Novak (Robotics & SMT Controls Engineer)", "alex.novak@factory.corp", "engineer", ["org-robotics"]),
    ("usr-tech-welding-1", "István Kovács (Laser Welding & Mechanical Tech)", "istvan.kovacs@factory.corp", "technician", ["org-assembly"]),

    # Candidates & Dedication Users (from technicianRulesStore.ts)
    ("usr-sally", "Engineer Sally", "sally.milling@heimdall.dev", "engineer", ["org-controls"]),
    ("usr-orwell", "Engineer Orwell", "orwell.audi@heimdall.dev", "group_leader", ["org-audi-proj"]),
    ("usr-katalin", "Katalin Nagy", "katalin.aoi@heimdall.dev", "group_leader", ["org-vision"]),
    ("usr-kovacs", "István Kovács", "istvan.kovacs@heimdall.dev", "technician", ["org-maintenance"]),
    ("usr-varga", "Gábor Varga", "gabor.varga@heimdall.dev", "technician", ["org-maintenance"]),
    ("usr-nemeth", "Zoltán Németh", "zoltan.nemeth@heimdall.dev", "technician", ["org-maintenance"]),
    ("usr-horvath", "Bence Horváth", "bence.horvath@heimdall.dev", "technician", ["org-maintenance"]),
    ("usr-pap", "Orsolya Pap", "orsolya.pap@heimdall.dev", "engineer", ["org-maintenance"]),

    # Ticketing Users (from initialTickets.ts)
    ("usr-op-01", "István Kovács (Operator)", "istvan.kovacs.op@factory.corp", "operator", ["org-line-01"]),
    ("usr-tech-01", "Gábor Varga (Lead Tech)", "gabor.varga.lead@factory.corp", "technician", ["org-maintenance"]),
    ("usr-op-02", "Zoltán Horváth", "zoltan.horvath.op@factory.corp", "operator", ["org-line-02"]),
    ("usr-tech-02", "Zoltán Németh", "zoltan.nemeth.tech@factory.corp", "technician", ["org-maintenance"]),
    ("usr-op-03", "Péter Nagy", "peter.nagy.op@factory.corp", "operator", ["org-line-03"]),
    ("usr-admin", "System Administrator", "admin@heimdall.local", "system_admin", ["org-platform"]),
    ("usr-op-04", "Tamás Szabó", "tamas.szabo.op@factory.corp", "operator", ["org-line-04"]),
    ("usr-tech-03", "Bence Horváth", "bence.horvath.tech@factory.corp", "technician", ["org-maintenance"]),
    ("usr-tech-04", "Orsolya Pap", "orsolya.pap.tech@factory.corp", "technician", ["org-maintenance"]),
    ("usr-op-05", "László Kiss", "laszlo.kiss.op@factory.corp", "operator", ["org-line-05"]),
    ("usr-op-06", "Balázs Farkas", "balazs.farkas.op@factory.corp", "operator", ["org-line-06"]),

    # Legacy Backend Technician Candidates
    ("tech-01", "Kovács István", "i.kovacs@heimdall.local", "technician", ["org-maintenance"]),
    ("tech-02", "Nagy Péter", "p.nagy@heimdall.local", "technician", ["org-maintenance"]),
    ("tech-03", "Szabó Tamás", "t.szabo@heimdall.local", "controls_engineer", ["org-controls"]),
    ("tech-04", "Varga Zoltán", "z.varga@heimdall.local", "engineer", ["org-robotics"]),
    ("tech-05", "Tóth Bence", "b.toth@heimdall.local", "lead_engineer", ["org-maintenance"]),
    ("tech-alice", "Alice Engineer", "alice.engineer@heimdall.local", "technician", ["org-controls"]),
    ("tech-sally", "Engineer Sally", "sally.tech@heimdall.dev", "engineer", ["org-controls"]),
    ("tech-orwell", "Engineer Orwell", "orwell.tech@heimdall.dev", "group_leader", ["org-audi-proj"]),
    ("tech-kovacs", "István Kovács", "kovacs.tech@heimdall.dev", "technician", ["org-maintenance"]),

    # Automation Bots & Dev
    ("usr-automation-bot", "Outside IT Automation Bot", "it-automation@heimdall.local", "system_admin", ["org-platform"]),
    ("outside-it-automation-bot", "Outside IT Automation Bot", "it-automation-service@heimdall.local", "system_admin", ["org-platform"]),
    ("usr-autonomous-agent", "Autonomous Fleet Telemetry (Dev)", "telemetry-agent@heimdall.local", "technician", ["org-platform"]),
    ("dev-admin-id", "Dev Administrator", "admin-dev@heimdall.local", "system_admin", ["org-platform"]),
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
            ("OP020", "Robo-RFID Pallet Scanner AI-X", "ConveyorTransfer", "Testing", "Balluff", "L01-OP020"),
            ("OP030", "Giga-Gluer 2K Thermal Dispenser Bot", "Dispenser", "Dispensing", "Nordson", "L01-OP030"),
            ("OP040", "Auto-Bolt Torquinator 3000", "FasteningStation", "Fastening", "Atlas Copco", "L01-OP040"),
            ("OP050", "Pallet-Lift Elevator Mech-Tron", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L01-OP050"),
            ("OP060", "Laser-Zapper Seam Welder Omni-9", "LaserWelder", "Welding", "Trumpf", "L01-OP060"),
            ("OP070", "High-Voltage Sparky Insulation Tester", "EOLTester", "Testing", "Chroma", "L01-OP070"),
            ("OP080", "Cell-Loader Gantry Manipulator AI", "RobotCell", "Robotics", "KUKA", "L01-OP080"),
            ("OP090", "Deep-Vision AOI Flaw-Finder 4000", "VisionInspection", "Testing", "Cognex", "L01-OP090"),
            ("OP100", "Multi-Spindle Cover Fastener Cyber", "FasteningStation", "Fastening", "Atlas Copco", "L01-OP100"),
            ("OP110", "Pallet Accumulator Buffer Loop-1", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L01-OP110"),
            ("OP120", "Outfeed Barcode Check & Gate Bot", "ConveyorTransfer", "Testing", "Keyence", "L01-OP120")
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
            ("OP070", "Laser Bead Profilometer Checker AI", "VisionInspection", "Testing", "Cognex", "L02-OP070"),
            ("OP080", "Helium Leak Sniffer Chamber Zero-P", "EOLTester", "Testing", "Pfeiffer", "L02-OP080"),
            ("OP090", "Pack Lid Structural Servo Press 50kN", "ServoPress", "Fastening", "TOX Pressotechnik", "L02-OP090"),
            ("OP100", "Perimeter Hex Bolt Nutrunner Gantry", "FasteningStation", "Fastening", "Atlas Copco", "L02-OP100"),
            ("OP110", "Optical Topographic Seal Inspector", "VisionInspection", "Testing", "Keyence", "L02-OP110"),
            ("OP120", "Final Pack Discharge Pallet Elevator", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L02-OP120"),
            ("OP130", "Battery Pack EOL High-Voltage Gate", "EOLTester", "Testing", "Chroma", "L02-OP130")
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
            ("OP070", "Infrared Resin Curing Tunnel EOL", "EOLTester", "Testing", "Heraeus", "L03-OP070"),
            ("OP080", "High-Speed Dynamic Balancing Rig 3D", "VisionInspection", "Testing", "Schenck", "L03-OP080"),
            ("OP090", "End-Shield Bearing Fastener Torquer", "FasteningStation", "Fastening", "Atlas Copco", "L03-OP090"),
            ("OP100", "Stator Rotor Magnetization Gate AI", "RobotCell", "Robotics", "KUKA", "L03-OP100"),
            ("OP110", "Powertrain Conveyor Turn-Table Loop", "ConveyorTransfer", "Assembly", "Bosch Rexroth", "L03-OP110"),
            ("OP120", "Powertrain Full Dynamometer Dyno-X", "EOLTester", "Testing", "AVL", "L03-OP120")
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
            ("OP020", "DEK Solder Paste Jet Printer 0.1mm", "VisionInspection", "Testing", "ASM", "L04-OP020"),
            ("OP030", "Koh Young 3D Solder Paste Inspector", "VisionInspection", "Testing", "Koh Young", "L04-OP030"),
            ("OP040", "Siplace Dual-Gantry Chip Shooter AI", "RobotCell", "Robotics", "ASM", "L04-OP040"),
            ("OP050", "Odd-Form Component Inserter Gantry", "Dispenser", "Dispensing", "Fuji", "L04-OP050"),
            ("OP060", "Pre-Reflow 3D Optical Vision Checker", "VisionInspection", "Testing", "Cognex", "L04-OP060"),
            ("OP070", "10-Zone Nitrogen Reflow Convection Oven", "EOLTester", "Testing", "Heller", "L04-OP070"),
            ("OP080", "Post-Reflow Automated Optical Inspector", "VisionInspection", "Testing", "Koh Young", "L04-OP080"),
            ("OP090", "Flying-Probe ICT Circuit Board Tester", "EOLTester", "Testing", "Spea", "L04-OP090"),
            ("OP100", "Selective Wave Solder Mini-Pot Bot", "FasteningStation", "Welding", "Ersa", "L04-OP100"),
            ("OP110", "Conformal Coating UV Dispenser Booth", "Dispenser", "Dispensing", "Nordson", "L04-OP110"),
            ("OP120", "UV Cure Inspection Tunnel & Camera", "VisionInspection", "Testing", "Cognex", "L04-OP120"),
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
            ("OP070", "Optical Body Gap & Flushness Gate", "VisionInspection", "Testing", "Cognex", "L05-OP070"),
            ("OP080", "Structural Foam Injection Applicator", "Dispenser", "Dispensing", "Nordson", "L05-OP080"),
            ("OP090", "Friction Stir Welder Overhead Unit", "FasteningStation", "Welding", "KUKA", "L05-OP090"),
            ("OP100", "Dual-Robot Seam Finishing Cell", "RobotCell", "Robotics", "Fanuc", "L05-OP100"),
            ("OP110", "Overhead Transfer Crane Pick Shifter", "ConveyorTransfer", "Assembly", "Demag", "L05-OP110"),
            ("OP120", "Body Shell Dimension Verification Gate", "VisionInspection", "Testing", "Keyence", "L05-OP120")
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
            ("OP070", "Force-Displacement Envelope Monitor", "VisionInspection", "Testing", "Promess", "L06-OP070"),
            ("OP080", "Ultrasonic Crack & Defect Detector", "EOLTester", "Testing", "Olympus", "L06-OP080"),
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
        "tech": "Testing",
        "station_count": 13,
        "stations": [
            ("OP010", "Inspection Pallet Indexing Rotary Bed", "ConveyorTransfer", "Assembly", "Weiss", "L07-OP010"),
            ("OP020", "Telecentric 50MP Multi-Angle Booth", "VisionInspection", "Testing", "Cognex", "L07-OP020"),
            ("OP030", "Laser Line Profilometer 3D Surface AI", "VisionInspection", "Testing", "Keyence", "L07-OP030"),
            ("OP040", "Surface Roughness Tactile Probing Arm", "RobotCell", "Robotics", "Mitutoyo", "L07-OP040"),
            ("OP050", "Specular Reflectance Defect Scanner", "VisionInspection", "Testing", "Cognex", "L07-OP050"),
            ("OP060", "Optical Coordinate Measuring CMM Gantry", "EOLTester", "Testing", "Zeiss", "L07-OP060"),
            ("OP070", "Deep-Learning Anomaly Classifier AI", "VisionInspection", "Testing", "Cognex", "L07-OP070"),
            ("OP080", "Part Re-Orientation Robotic Swivel", "RobotCell", "Robotics", "KUKA", "L07-OP080"),
            ("OP090", "X-Ray CT Void Inspection Scanner", "VisionInspection", "Testing", "Yxlon", "L07-OP090"),
            ("OP100", "Direct Part Marking Laser QR Stamper", "FasteningStation", "Welding", "Trumpf", "L07-OP100"),
            ("OP110", "Post-Marking Verification Scanner 2D", "VisionInspection", "Testing", "Cognex", "L07-OP110"),
            ("OP120", "Certified Quality Pallet Stacking Bay", "ConveyorTransfer", "Assembly", "Demag", "L07-OP120"),
            ("OP130", "Automated Metrology Archive & Gate", "ConveyorTransfer", "Testing", "Keyence", "L07-OP130")
        ]
    },
    {
        "line_num": 8,
        "org_id": "org-line-08",
        "name": "Line 08 – Synthetic End-of-Line Vehicle Integration (AI Sim)",
        "tech": "Testing",
        "station_count": 13,
        "stations": [
            ("OP010", "Final Chasis Docking & Lock Conveyor", "ConveyorTransfer", "Assembly", "Siemens", "L08-OP010"),
            ("OP020", "Brake Hydraulic Vacuum Bleed Bench", "EOLTester", "Testing", "Dürr", "L08-OP020"),
            ("OP030", "Coolant Degassing & Fluid Filling Station", "EOLTester", "Testing", "Dürr", "L08-OP030"),
            ("OP040", "Wheel Nut Automated Torque Tightener 5X", "RobotCell", "Robotics", "Atlas Copco", "L08-OP040"),
            ("OP050", "Optical Wheel Alignment Laser Gate", "VisionInspection", "Testing", "Beissbarth", "L08-OP050"),
            ("OP060", "Headlight Matrix LED Calibration Rig", "EOLTester", "Testing", "Hella", "L08-OP060"),
            ("OP070", "ADAS Radar & LiDAR Target Array Aligner", "FasteningStation", "Testing", "Continental", "L08-OP070"),
            ("OP080", "All-Wheel Drive Roll Dynamometer 150kW", "EOLTester", "Testing", "Maha", "L08-OP080"),
            ("OP090", "OBD-II High-Speed Ethernet Flash ECU", "RobotCell", "Robotics", "Vector", "L08-OP090"),
            ("OP100", "Underbody Acoustic Ultrasonic Sniffer", "VisionInspection", "Testing", "Siemens", "L08-OP100"),
            ("OP110", "Monsoon Water Ingress Leak Test Booth", "EOLTester", "Testing", "Dürr", "L08-OP110"),
            ("OP120", "Hot Air Blow-Off & Drying Tunnel Bot", "ConveyorTransfer", "Assembly", "Dürr", "L08-OP120"),
            ("OP130", "Final Shipping Factory Release Gate", "ConveyorTransfer", "Testing", "Siemens", "L08-OP130")
        ]
    }
]


# ======================================================================================
# Enterprise plant model
# --------------------------------------------------------------------------------------
# Everything below derives ONE in-memory model (`build_model`) from the constants above and
# renders the four generated artifacts from it:
#
#   seed_data/inventory_seed.csv             inventory master data (machines, IPCs, parts)
#   seed_data/production_topology.json       lines -> stations -> controllers (+ components)
#   seed_data/incremental_seed.sql           transactional reseed of the dev database
#   fixtures/enterprise_plant_dataset.json   directory / identity fixture (BFF, AD mock, simulators)
#
# The seed contains reference / master data only. Activity (station telemetry, IPC heartbeats,
# tickets, spare-part consumption) is produced by the fleet simulator through MQTT and the REST
# API; see docs/guide/DEV_GUIDE.md §3.5.
# ======================================================================================

SEED = 42
FIXTURE_FILE = os.path.join(REPO_ROOT, 'fixtures', 'enterprise_plant_dataset.json')

PLANT_NAME = "Smart Factory Giga-01 (AI Synthetic Facility)"
PLANT_CODE = "SF-GIGA-01"
PLANT_DOMAIN = "fake-factory.internal"

# Dev admin account contract (also created by the Nuxt dev plugin): do not change.
ADMIN_USER = {"id": "usr-admin-primary", "name": "System Administrator", "email": "admin@heimdall.dev",
              "role": "system_admin", "username": "admin"}
ADMIN_PASSWORD_HASH = ("4ad2888535f37c17fa6b2b7d74dc6211:bdb17beff0b6883c8f7d0ca3485b8dc4e7b17205c2f6ff4f77d4cbf41a1291181"
                       "cb4880a34aac47c81e8d171980522b9579ae046e512166eebc239242a8a0900")

DISCIPLINES = {"Mechanical", "Controls", "Electrical", "Safety", "Quality", "Process", "Tooling", "General"}
SKILL_CATEGORIES = {"Certification", "TechnologyKnowHow", "MachineAuthorization", "SafetyClearance"}
EQUIPMENT_STATUSES = {"InStorage", "InMachine", "UnderRepair", "Decommissioned", "Scrapped"}
COMPONENT_CATEGORIES = {"Hardware", "SoftwareBinary", "SoftwareRuntime", "SoftwareConfig", "SoftwareProgram",
                        "SoftwareRecipe", "License", "Document"}
# Reserved / non-routable mail domains used by the synthetic identities.
FAKE_EMAIL_DOMAINS = ("fake-factory.internal", "heimdall.dev", "heimdall.local", "factory.corp")


def uid(key):
    """Deterministic id (same derivation the dataset has always used: uuid5 of the row name)."""
    return str(uuid.uuid5(uuid.NAMESPACE_DNS, key))


def _rng(key):
    return random.Random(f"{SEED}:{key}")


# --------------------------------------------------------------------------------------
# Part classes: what can be installed in a station and which serialized spare replaces it.
# `spare` is None for software, which is never swapped from the store.
# --------------------------------------------------------------------------------------
PART_CLASSES = {
    "PLC-CPU": dict(code="PLC-S7-1516F", name="Siemens S7-1516F Safety PLC", mfr="Siemens", tech="Controls", kind="HardwareComponent",
                    category="Hardware", tags=["PLC", "Safety"], meta={"Model": "1516F-3 PN/DP", "Safety": "SIL3", "Rack": 1},
                    spare=("SIEM-S7-1500", "Siemens S7-1516F Safety PLC CPU (Spare)", 2850000)),
    "SERVO-DRIVE": dict(code="DRV-AX5118", name="Beckhoff AX5118 Servo Drive 18A", mfr="Beckhoff", tech="Robotics", kind="HardwareComponent",
                        category="Hardware", tags=["Servo", "EtherCAT"], meta={"Current": "18A", "Feedback": "OCT One Cable", "Channels": 1},
                        spare=("BECK-AX5000", "Beckhoff AX5118 Servo Drive 18A (Spare)", 1450000)),
    "VISION-CAM": dict(code="CAM-Cognex-9912", name="Cognex In-Sight 9912 12MP Camera", mfr="Cognex", tech="Testing", kind="HardwareComponent",
                       category="Hardware", tags=["Vision"], meta={"Resolution": "12MP", "Lens": "25mm", "Illumination": "Polarized Red"},
                       spare=("COGN-IN9912", "Cognex In-Sight 9912 12MP Inspection Camera (Spare)", 2200000)),
    "ROBOT-CTRL": dict(code="ROB-KUKA-KRC4", name="KUKA KRC4 Robot Controller PC", mfr="KUKA", tech="Robotics", kind="HardwareComponent",
                       category="Hardware", tags=["Robot", "Controller"], meta={"Robot": "KR210 R2700", "Payload": "210kg", "Controller": "KRC4"},
                       spare=("KUKA-KRC4-PC", "KUKA KRC4 Industrial Robot Controller PC (Spare)", 3900000)),
    "VALVE-TERMINAL": dict(code="VAL-Festo-VTUG", name="Festo VTUG Multi-Valve Terminal 16X", mfr="Festo", tech="Dispensing", kind="HardwareComponent",
                           category="Hardware", tags=["Pneumatics", "Dosing"], meta={"Valves": 16, "Bus": "Profinet", "AirPressure": "6.5 Bar"},
                           spare=("FEST-VTUG-16", "Festo VTUG Valve Terminal 16-Valve Multi (Spare)", 850000)),
    "TIGHTENING-SPINDLE": dict(code="SPN-Atlas-STR61", name="Atlas Copco Tensor STR-61 Tightening Spindle", mfr="Atlas Copco", tech="Fastening",
                               kind="HardwareComponent", category="Hardware", tags=["Torque", "WearPart"],
                               meta={"TorqueRangeNm": "8-40", "Transducer": "Dual", "CalibrationIntervalCycles": 250000},
                               spare=("ATLAS-STR-61", "Atlas Copco Tensor STR-61 Tightening Spindle (Spare)", 3100000)),
    "SAFETY-SCANNER": dict(code="SCN-SICK-microScan3", name="SICK microScan3 Core Safety Laser Scanner", mfr="SICK", tech="Robotics",
                           kind="HardwareComponent", category="Hardware", tags=["Safety", "Scanner"],
                           meta={"ProtectiveField": "5.5m", "Interface": "EFI-pro", "PerformanceLevel": "PLd"},
                           spare=("SICK-MICROSCAN", "SICK microScan3 Core Safety Laser Scanner (Spare)", 1250000)),
    "DRIVE-INVERTER": dict(code="INV-SEW-MOVIPRO", name="SEW Eurodrive MOVIPRO Decentralized Inverter", mfr="SEW Eurodrive", tech="Assembly",
                           kind="HardwareComponent", category="Hardware", tags=["Drive", "Conveyor"],
                           meta={"PowerKw": 2.2, "Bus": "Profinet", "Brake": "24V"},
                           spare=("SEW-MOVIPRO", "SEW Eurodrive MOVIPRO Decentralized Inverter (Spare)", 1600000)),
    "RFID-READER": dict(code="RFID-Balluff-BISM", name="Balluff BIS-M 13.56MHz RFID Read Head", mfr="Balluff", tech="Assembly",
                        kind="HardwareComponent", category="Hardware", tags=["Identification", "Pallet"],
                        meta={"Frequency": "13.56MHz", "Interface": "IO-Link"},
                        spare=("BALL-RFID-RWD", "Balluff BIS-M 13.56MHz High-Temp RFID Reader (Spare)", 480000)),
    "LASER-OPTIC": dict(code="OPT-Trumpf-BEO-D70", name="Trumpf BEO D70 Laser Focus Optic Head", mfr="Trumpf", tech="Welding",
                        kind="HardwareComponent", category="Hardware", tags=["Laser", "Optics", "WearPart"],
                        meta={"FocalLengthMm": 250, "ProtectiveGlass": "D37 x 1.5 mm", "MaxPowerKw": 6},
                        spare=("TRUMPF-OPTIC", "Trumpf BEO D70 Laser Seam Optical Focus Head (Spare)", 4500000)),
    "PRESS-DRIVE": dict(code="PRS-TOX-EDrive50", name="TOX ElectricDrive 50kN Servo Press Drive", mfr="TOX Pressotechnik", tech="Fastening",
                        kind="HardwareComponent", category="Hardware", tags=["Press", "Servo"],
                        meta={"ForceKn": 50, "StrokeMm": 150, "ForceSensor": "Piezo"},
                        spare=("TOX-E-PRESS", "TOX ElectricDrive 50kN Servo Press Drive (Spare)", 4800000)),
    "NET-SWITCH": dict(code="SWI-Hirschmann-BOBCAT", name="Hirschmann BOBCAT Managed Switch", mfr="Hirschmann", tech="Controls",
                       kind="HardwareComponent", category="Hardware", tags=["Network"], meta={"Ports": 12, "PoE": True, "Speed": "2.5Gbps"},
                       spare=("HIRS-BOBCAT", "Hirschmann BRS20 Industrial Managed Switch (Spare)", 720000)),
    "SW-PLC-RUNTIME": dict(code="SW-TwinCAT3-RT", name="Beckhoff TwinCAT 3 PLC Runtime License", mfr="Beckhoff", tech="Controls",
                           kind="SoftwareComponent", category="SoftwareRuntime", tags=["PLC", "Runtime"],
                           meta={"LicenseKey": "TC3-SYNTH-ENTERPRISE-100", "Version": "3.1.4026.10"}, spare=None),
    "SW-MES-CONNECTOR": dict(code="SW-Opcenter-MES", name="Siemens Opcenter MES Edge Connector", mfr="Siemens", tech="Controls",
                             kind="SoftwareComponent", category="SoftwareConfig", tags=["MES"],
                             meta={"LicenseKey": "OPC-MES-SYNTH-V4", "Version": "4.2.0"}, spare=None),
}
SPARE_CLASS_ORDER = [c for c, v in PART_CLASSES.items() if v["spare"]]

# Machine type catalogue: nominal cycle time, owning team, and the two installed components
# (part class, functional slot = mesh name of the digital twin).
MACHINE_TYPES = {
    "ConveyorTransfer": dict(cycle_s=16, team="Tooling & Mechanoids (Synthetic AI Guild)", safety="SIL2 / PLd", power="400V 3Ph 50Hz",
                             components=[("DRIVE-INVERTER", "CONVEYOR-01"), ("RFID-READER", "VISION-CAM-01")]),
    "Dispenser": dict(cycle_s=22, team="Tooling & Mechanoids (Synthetic AI Guild)", safety="SIL2 / PLd", power="400V 3Ph 50Hz",
                      components=[("VALVE-TERMINAL", "TOOL-HEAD-01"), ("SW-PLC-RUNTIME", "PANEL-STATUS-01")]),
    "FasteningStation": dict(cycle_s=18, team="Tooling & Mechanoids (Synthetic AI Guild)", safety="SIL2 / PLd", power="400V 3Ph 50Hz",
                             components=[("TIGHTENING-SPINDLE", "TOOL-HEAD-01"), ("PLC-CPU", "PANEL-STATUS-01")]),
    "LaserWelder": dict(cycle_s=34, team="Robotics & Cybernetics (Synthetic AI Guild)", safety="SIL3 / PLe", power="400V 3Ph 50Hz",
                        components=[("LASER-OPTIC", "TOOL-HEAD-01"), ("SERVO-DRIVE", "GANTRY-X-01")]),
    "EOLTester": dict(cycle_s=42, team="Plant Maintenance (Synthetic AI Guild)", safety="SIL2 / PLd", power="400V 3Ph 50Hz",
                      components=[("SERVO-DRIVE", "GANTRY-X-01"), ("NET-SWITCH", "PANEL-STATUS-01")]),
    "RobotCell": dict(cycle_s=28, team="Robotics & Cybernetics (Synthetic AI Guild)", safety="SIL3 / PLe", power="400V 3Ph 50Hz",
                      components=[("ROBOT-CTRL", "GANTRY-X-01"), ("SAFETY-SCANNER", "VISION-CAM-01")]),
    "VisionInspection": dict(cycle_s=9, team="Vision & Photons (Synthetic AI Guild)", safety="SIL2 / PLd", power="24V DC Industrial Spool",
                             components=[("VISION-CAM", "VISION-CAM-01"), ("SW-MES-CONNECTOR", "PANEL-STATUS-01")]),
    "ServoPress": dict(cycle_s=20, team="Tooling & Mechanoids (Synthetic AI Guild)", safety="SIL3 / PLe", power="400V 3Ph 50Hz",
                       components=[("PRESS-DRIVE", "TOOL-HEAD-01"), ("SERVO-DRIVE", "GANTRY-Z-01")]),
}
SMT_TEAM = "SMT & Microchips (Synthetic AI Guild)"

# Technological machine groups (cross-line fleets of the same machine type).
TECH_GROUPS = [
    ("tech-conveyor", "Conveyors & Pallet Transfer Fleet (Synthetic)", ["ConveyorTransfer"], "Assembly", "slate", "ArrowRightLeft"),
    ("tech-dispensing", "Dispensing Cell Fleet (Synthetic)", ["Dispenser"], "Dispensing", "teal", "Droplets"),
    ("tech-fastening", "Fastening & Press-Fit Fleet (Synthetic)", ["FasteningStation", "ServoPress"], "Fastening", "orange", "Wrench"),
    ("tech-laser", "Laser Welding Fleet (Synthetic)", ["LaserWelder"], "Welding", "red", "Zap"),
    ("tech-robot", "Robot Cell Fleet (Synthetic)", ["RobotCell"], "Robotics", "violet", "Bot"),
    ("tech-vision", "Vision & Quality Gate Fleet (Synthetic)", ["VisionInspection"], "Testing", "cyan", "ScanEye"),
    ("tech-eol", "End-of-Line Test Bench Fleet (Synthetic)", ["EOLTester"], "Testing", "blue", "Gauge"),
]

# Projects group the lines; the business unit groups the projects.
PROJECTS = [
    ("proj-audi", "Audi Vehicle Project (Synthetic AI Guild)", "Synthetic module, battery pack and powertrain lines (L01-L03)", (1, 2, 3), "usr-synth-19"),
    ("proj-platform", "Electronics, Body & Final Assembly Programme (Synthetic)", "Synthetic SMT, body-in-white, press-fit, metrology and EOL lines (L04-L08)", (4, 5, 6, 7, 8), "usr-synth-22"),
]
SUB_LINE_OPS = ("OP080", "OP090")   # the m-n robot cell of every line is its sub-line
BUSINESS_UNIT = ("bu-giga01", PLANT_NAME, "Synthetic business unit owning every Giga-01 production line", "usr-synth-06")

# Skills (fictional internal certificates).
SKILLS = [
    ("SYN-LASER-CL4", "Class 4 Laser Safety & Optics Service", "May open laser cells, clean and replace optics", "SafetyClearance", "Giga-01 EHS Office (Synthetic)", 24),
    ("SYN-PLC-61131", "IEC 61131-3 PLC Troubleshooting", "Online diagnosis and controlled changes on line PLC programs", "TechnologyKnowHow", "Controls Guild Academy (Synthetic)", 36),
    ("SYN-EL-QUAL", "Electrically Qualified Person", "Work on 400 V cabinets under supervision rules", "SafetyClearance", "Giga-01 EHS Office (Synthetic)", 12),
    ("SYN-ROBOT-L2", "Robot Cell Programming Authorization", "Teach-pendant authorization for robot cells", "MachineAuthorization", "Robotics Guild Academy (Synthetic)", 24),
    ("SYN-TORQUE-CAL", "Tightening System Calibration", "Transducer verification and torque audit of nutrunner spindles", "Certification", "Tooling Guild Academy (Synthetic)", 24),
    ("SYN-DISP-2K", "2K Dispensing Process Service", "Mixer, dosing valve and material supply service", "TechnologyKnowHow", "Tooling Guild Academy (Synthetic)", 36),
    ("SYN-VISION", "Vision Recipe Engineering", "Camera recipe changes, lighting and calibration for quality gates", "Certification", "Vision Guild Academy (Synthetic)", 24),
]

# Maintenance crews. The name carries the technology keyword the backend's dispatch logic matches on.
# (key, name, description, discipline, shift pattern, members, skill held by every member)
PERSONNEL_GROUPS = [
    ("pg-dispensing", "Dispensing & Fluid Process Crew (Synthetic)", "Dosing valves, mixers and material supply", "Process", "Shift 1",
     ["usr-synth-55", "usr-synth-32", "usr-synth-39", "usr-synth-25"], "SYN-DISP-2K"),
    ("pg-fastening", "Fastening & Press-Fit Crew (Synthetic)", "Nutrunners, servo presses and joining tools", "Tooling", "Shift 1",
     ["usr-synth-31", "usr-synth-51", "usr-synth-40", "usr-synth-46"], "SYN-TORQUE-CAL"),
    ("pg-welding", "Welding & Laser Crew (Synthetic)", "Laser cells, optics and weld fixtures", "Process", "Shift 1",
     ["usr-synth-23", "usr-synth-56", "usr-synth-45", "usr-synth-33"], "SYN-LASER-CL4"),
    ("pg-robotics", "Robotics Crew (Synthetic)", "Robot cells, grippers and safety scanners", "Tooling", "Shift 1",
     ["usr-synth-52", "usr-synth-10", "usr-synth-24", "usr-synth-22"], "SYN-ROBOT-L2"),
    ("pg-assembly", "Assembly & Conveyor Crew (Synthetic)", "Pallet conveyors, lifts and mechanical assembly stations", "General", "Shift 1",
     ["usr-synth-26", "usr-synth-15", "usr-synth-54", "usr-synth-59"], None),
    ("pg-testing", "Testing & Vision Crew (Synthetic)", "Vision gates, test benches and measuring equipment", "Quality", "Shift 1",
     ["usr-synth-18", "usr-synth-48", "usr-synth-44", "usr-synth-27", "usr-synth-13"], "SYN-VISION"),
    ("pg-controls", "Controls & Fieldbus On-Call (Synthetic)", "24/7 rota for PLC, fieldbus and IPC incidents", "Controls", "24/7 Continental",
     ["usr-synth-20", "usr-synth-37", "usr-synth-42", "usr-synth-02", "usr-synth-01"], "SYN-PLC-61131"),
    ("pg-electrical", "Electrical & Safety Crew (Synthetic)", "Cabinets, drives, safety circuits", "Electrical", "Shift 1",
     ["usr-synth-11", "usr-synth-28", "usr-synth-34", "usr-synth-57"], "SYN-EL-QUAL"),
    ("pg-escalation", "Plant Engineering Escalation (Synthetic)", "Second level: shift leader, lead engineers, plant engineering manager", "Safety", "OfficeHours",
     ["usr-synth-09", "usr-synth-22", "usr-synth-04", "usr-synth-06"], None),
]

# Dispatch rules. One technology-primary recipe per station technology (global), plus Controls and
# Electrical recipes that target every line group and therefore win for PLC / electrical alarms.
# (key, name, description, discipline, technology, crew, on-call fallback user, required skill, targets lines)
DELEGATION_RECIPES = [
    ("dr-dispensing", "Dispensing Dispatch (Synthetic)", "Faults on dispensing cells go to the dispensing crew", "Process", "Dispensing", "pg-dispensing", "usr-synth-20", "SYN-DISP-2K", False),
    ("dr-fastening", "Fastening & Press-Fit Dispatch (Synthetic)", "Faults on nutrunners and servo presses go to the fastening crew", "Tooling", "Fastening", "pg-fastening", "usr-synth-20", "SYN-TORQUE-CAL", False),
    ("dr-welding", "Laser Welding Dispatch (Synthetic)", "Faults on laser welding cells go to the laser crew", "Process", "Welding", "pg-welding", "usr-synth-37", "SYN-LASER-CL4", False),
    ("dr-robotics", "Robot Cell Dispatch (Synthetic)", "Faults on robot cells go to the robotics crew", "Tooling", "Robotics", "pg-robotics", "usr-synth-37", "SYN-ROBOT-L2", False),
    ("dr-assembly", "Assembly & Conveyor Dispatch (Synthetic)", "Faults on conveyors and assembly stations go to the assembly crew", "General", "Assembly", "pg-assembly", "usr-synth-42", None, False),
    ("dr-testing", "Testing & Vision Dispatch (Synthetic)", "Faults on vision gates and test benches go to the testing crew", "Quality", "Testing", "pg-testing", "usr-synth-42", "SYN-VISION", False),
    ("dr-controls", "Controls / PLC Incident Dispatch (Synthetic)", "PLC, fieldbus and IPC incidents on every line go to the controls on-call rota", "Controls", "Controls", "pg-controls", "usr-synth-20", "SYN-PLC-61131", True),
    ("dr-electrical", "Electrical Incident Dispatch (Synthetic)", "Electrical incidents on every line go to the electrical crew", "Electrical", "Electrical", "pg-electrical", "usr-synth-11", "SYN-EL-QUAL", True),
]
ESCALATION_GROUP = "pg-escalation"

# Bulk consumables: (name, display, manufacturer, technology, quantity, minimum, bin, consumable class)
BULK_ITEMS = [
    ("STK-PNEU-QS4", "Simulated Festo QS-1/8-4 Quick Push-In Fittings", "Festo", "Assembly", 180, 40, "Bin PNEU-01", "PNEU-FITTING"),
    ("STK-PNEU-QS6", "Simulated Festo QS-1/4-6 Quick Couplers", "Festo", "Assembly", 240, 50, "Bin PNEU-02", "PNEU-FITTING"),
    ("STK-PNEU-QS8", "Simulated Festo QS-1/4-8 Pneumatic Fittings", "Festo", "Assembly", 310, 60, "Bin PNEU-03", "PNEU-FITTING"),
    ("STK-PNEU-QS10", "Simulated Festo QS-3/8-10 Heavy Pneumatic Couplers", "Festo", "Assembly", 140, 30, "Bin PNEU-04", "PNEU-FITTING"),
    ("STK-TUBE-PU8-BL", "Simulated Festo PUN-H-8x1.25 Blue Tubing (50m Roll)", "Festo", "Assembly", 25, 5, "Bin PNEU-05", "PNEU-TUBE"),
    ("STK-TUBE-PU6-BK", "Simulated Festo PUN-H-6x1.0 Black Tubing (50m Roll)", "Festo", "Assembly", 30, 8, "Bin PNEU-06", "PNEU-TUBE"),
    ("STK-SCRW-M3-10", "Simulated Würth DIN 912 M3x10mm Socket Head Screws (100x Box)", "Würth", "Fastening", 85, 20, "Bin FAST-01", "FASTENER"),
    ("STK-SCRW-M4-16", "Simulated Würth DIN 912 M4x16mm Socket Head Screws (100x Box)", "Würth", "Fastening", 120, 25, "Bin FAST-02", "FASTENER"),
    ("STK-SCRW-M5-20", "Simulated Würth DIN 912 M5x20mm High-Tensile Bolts (100x Box)", "Würth", "Fastening", 95, 20, "Bin FAST-03", "FASTENER"),
    ("STK-SCRW-M6-25", "Simulated Würth DIN 912 M6x25mm Grade 8.8 Screws (100x Box)", "Würth", "Fastening", 150, 30, "Bin FAST-04", "FASTENER"),
    ("STK-SCRW-M8-30", "Simulated Würth DIN 912 M8x30mm Grade 10.9 Bolts (50x Box)", "Würth", "Fastening", 80, 20, "Bin FAST-05", "FASTENER"),
    ("STK-NUT-M8-FLG", "Simulated Würth DIN 6923 M8 Hex Flange Locking Nuts (100x Box)", "Würth", "Fastening", 110, 25, "Bin FAST-06", "FASTENER"),
    ("STK-BOLT-M10-40", "Simulated Würth DIN 7991 M10x40mm Countersunk Hex Bolts (50x)", "Würth", "Fastening", 60, 15, "Bin FAST-07", "FASTENER"),
    ("STK-ALUM-4040-1M", "Simulated Bosch Rexroth 40x40 Strut Profile (1000mm Cut)", "Bosch Rexroth", "Assembly", 45, 10, "Rack STRUT-A1", "STRUT"),
    ("STK-ALUM-4040-2M", "Simulated Bosch Rexroth 40x40 Strut Profile (2000mm Bar)", "Bosch Rexroth", "Assembly", 35, 8, "Rack STRUT-A2", "STRUT"),
    ("STK-ALUM-4545-1M", "Simulated Bosch Rexroth 45x45 Heavy Modular Strut (1000mm)", "Bosch Rexroth", "Assembly", 50, 12, "Rack STRUT-B1", "STRUT"),
    ("STK-ALUM-4590-2M", "Simulated Bosch Rexroth 45x90 Structural Beam (2000mm Bar)", "Bosch Rexroth", "Assembly", 20, 5, "Rack STRUT-B2", "STRUT"),
    ("STK-ALUM-BRKT-40", "Simulated Bosch Rexroth 40x40 Cast Aluminum Corner Brackets", "Bosch Rexroth", "Assembly", 250, 50, "Bin STRUT-C1", "STRUT"),
    ("STK-ALUM-TNUT-M8", "Simulated Bosch Rexroth M8 T-Slot Roll-In Spring Nuts (100x)", "Bosch Rexroth", "Assembly", 180, 40, "Bin STRUT-C2", "STRUT"),
    ("STK-TERM-PT25", "Simulated Phoenix Contact PT 2.5 DIN Rail Terminal Blocks", "Phoenix Contact", "Assembly", 350, 80, "Bin ELEC-01", "ELEC-TERMINAL"),
    ("STK-FERR-15MM", "Simulated Phoenix Contact Insulated Wire Ferrules 1.5mm² (500x)", "Phoenix Contact", "Assembly", 90, 20, "Bin ELEC-02", "ELEC-TERMINAL"),
    ("STK-TIE-200MM", "Simulated UV-Resistant Industrial Zip Cable Ties 200mm (500x)", "HellermannTyton", "Assembly", 140, 30, "Bin ELEC-03", "CABLE-TIE"),
    ("STK-DISP-NZ25", "Simulated Nordson Precision Dispenser Nozzles 0.25mm Luer Lock", "Nordson", "Dispensing", 65, 15, "Bin DISP-01", "DISP-NOZZLE"),
]

SECURITY_GROUP_MAPPINGS = [
    ("EntraID", "sg-entra-admins", "OT Plant Administrators", "system_admin", "org-platform"),
    ("ActiveDirectory", "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp", "Line 01 Controls Engineers", "controls_engineer", "org-line-01"),
    ("ActiveDirectory", "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp", "Line 02 Assembly Techs", "technician", "org-line-02"),
    ("ActiveDirectory", "CN=SG-Line03-Powertrain,OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp", "Line 03 Powertrain Engineers", "engineer", "org-line-03"),
    ("ActiveDirectory", "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp", "Line 04 SMT Placement Leads", "lead_engineer", "org-line-04"),
    ("ActiveDirectory", "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp", "Line 05 Robotic Welders", "engineer", "org-line-05"),
    ("ActiveDirectory", "CN=SG-Line06-PressFit,OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp", "Line 06 Press Fit Techs", "technician", "org-line-06"),
    ("ActiveDirectory", "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp", "Line 07 Quality Metrologists", "lead_engineer", "org-line-07"),
    ("ActiveDirectory", "CN=SG-Line08-EOL,OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp", "Line 08 EOL Vehicle Techs", "technician", "org-line-08"),
    ("ActiveDirectory", "CN=SG-Plant-Maintenance,OU=Maintenance,OU=EngineeringDisciplines,DC=factory,DC=corp", "Plant Maintenance Specialists", "technician", "org-maintenance"),
]
AD_OU_GOVERNANCE = [f"OU=AudiLine{n:02d},OU=ProductionLines,DC=factory,DC=corp" for n in range(1, 9)] + ["OU=ControlsEngineering,DC=factory,DC=corp"]
OU_APPROVER = "dr.algorithmus.prime.ai@fake-factory.internal"

CSV_FIELDS = [
    "Type", "Name", "DisplayName", "ResponsibleTeam", "Manufacturer", "Supplier",
    "SerialNumber", "ParentName", "StationIdentifier", "ClientPcHostname", "Metadata",
    "PinnedObjectHandle", "OrganizationId", "StorageLocation", "EquipmentStatus",
    "MachineId", "StockQuantity", "MinStockThreshold", "IsStockItem", "Technology",
    "MachineType", "GroupId",
]


def line_code(line_num):
    return f"L{line_num:02d}"


def member_role(role):
    if role in ("system_admin", "heimdall_admin", "plant_director"):
        return "owner"
    if role in ("plant_engineering_manager", "senior_engineering_manager", "it_admin", "engineering_admin",
                "lead_engineer", "shift_leader", "manager"):
        return "admin"
    return "member"


def _supplier_for(manufacturer):
    """Stable manufacturer -> supplier assignment (no randomness)."""
    return SUPPLIERS[sum(manufacturer.encode("utf-8")) % len(SUPPLIERS)]


def _line_ipcs(l_cfg):
    """The 7 IPCs of a line and the stations each one controls (4 control topologies)."""
    n = l_cfg["line_num"]
    lc = line_code(n)
    ops = [s[0] for s in l_cfg["stations"]]
    special = {"OP030", "OP060", "OP080", "OP090"}
    line_plc_stations = [op for op in ops if op not in special]
    return [
        # 1-1: dedicated controller of one station
        (f"IPC-{lc}-OP030-DEDICATED", "Dedicated 1:1 Controller", "OP030", "Beckhoff", "Windows 10 IoT Enterprise 2021 LTSC",
         f"TC3_Line{n:02d}_Cell030.tsproj (Port 851)", "Audi MES Production Client v3.1", "Embedded PC C6030 Fanless",
         [("OP030", "Primary")]),
        # 1-n: the line master PLC runs the pallet conveyor and every standard station
        (f"IPC-{lc}-CONVEYOR-MAIN", "Line Master 1:n PLC - conveyor and standard stations", "OP010", "Siemens", "Windows 11 IoT Enterprise LTSC 2024",
         f"TC3_Line{n:02d}_PalletLoop.tsproj (Port 851)", "Siemens Opcenter Connector v4.2", "SIMATIC IPC477E Industrial",
         [(op, "Primary") for op in line_plc_stations]),
        # m-n: two robot controllers share two coordinated stations
        (f"IPC-{lc}-ROB-ALPHA", "Distributed m:n Cell", "OP080", "KUKA", "Windows 10 IoT Enterprise",
         f"TC3_Line{n:02d}_RoboticsShared.tsproj (Port 851)", "KUKA KRC4 Edge Telemetry Connector", "Advantech UNO-2484G Fanless Edge",
         [("OP080", "Primary"), ("OP090", "Primary")]),
        (f"IPC-{lc}-ROB-BETA", "Distributed m:n Cell", "OP080", "KUKA", "Windows 10 IoT Enterprise",
         f"TC3_Line{n:02d}_RoboticsShared.tsproj (Port 851)", "KUKA KRC4 Edge Telemetry Connector", "Advantech UNO-2484G Fanless Edge",
         [("OP080", "Secondary"), ("OP090", "Secondary")]),
        # n-1: three specialised IPCs on one complex station
        (f"IPC-{lc}-OP060-PLC", "Motion & Safety PLC", "OP060", "Beckhoff", "Windows 11 IoT Enterprise",
         f"TC3_Line{n:02d}_LaserMotion.tsproj (Port 851)", "Beckhoff ADS Telemetry Streamer", "Embedded PC C6032 High-Perf",
         [("OP060", "Primary")]),
        (f"IPC-{lc}-OP060-VISION", "Real-time Vision Inspector", "OP060", "Cognex", "Windows 10 IoT Enterprise",
         "Vision GigE Cam Driver active", "Cognex In-Sight Explorer 6.5.0 Connector", "Dell OptiPlex 7090 Micro Edge",
         [("OP060", "Vision")]),
        (f"IPC-{lc}-OP060-MES-GATE", "Plant MES Integration Gateway", "OP060", "Advantech", "Debian 12 Bookworm Industrial",
         "MQTT / OPC UA Gateway active", "Audi Corporate Plant MES Gateway v3", "Advantech UNO-2271G Edge",
         [("OP060", "Gateway")]),
    ]


def build_model():
    """The complete enterprise dataset as plain dicts/lists. Pure and deterministic."""
    users = {}
    for u_id, name, email, role, title, dept, orgs in FAKE_USERS:
        users[u_id] = dict(id=u_id, name=name, email=email, role=role, title=title, department=dept, orgs=list(orgs),
                           username=email.split("@")[0], kind="persona")
    for u_id, name, email, role, orgs in ADDITIONAL_SYSTEM_USERS:
        users[u_id] = dict(id=u_id, name=name, email=email, role=role, title=None, department=None, orgs=list(orgs),
                           username=u_id.lower().replace("-", "_"), kind="system")
    org_ids = [o["id"] for o in ORGANIZATIONS]

    crew_members = {m for g in PERSONNEL_GROUPS if g[0] != ESCALATION_GROUP for m in g[5]}

    def line_staff(org_id):
        """Line owner (lead engineer of the line organization) and SPOC (a crew technician of that organization)."""
        members = [u for u in users.values() if u["kind"] == "persona" and org_id in u["orgs"]]
        lead = next((u for u in members if u["role"] == "lead_engineer"), None) \
            or next((u for u in members if u["role"] in ("engineer", "controls_engineer")), None)
        technicians = [u for u in members if u["role"] == "technician"]
        spoc = next((u for u in technicians if u["id"] in crew_members), None) \
            or next(iter(technicians), None) \
            or next((u for u in members if u["id"] in crew_members and u is not lead), None) \
            or lead
        return lead, spoc

    # ---------------------------------------------------------------- lines & stations
    lines, stations = [], []
    for l_cfg in LINE_CONFIGS:
        n = l_cfg["line_num"]
        lead, spoc = line_staff(l_cfg["org_id"])
        project = next(p for p in PROJECTS if n in p[3])
        line = dict(line_num=n, code=line_code(n), org_id=l_cfg["org_id"], name=l_cfg["name"], tech=l_cfg["tech"],
                    group_key=f"grp-line-{n:02d}", group_id=uid(f"machine-group:grp-line-{n:02d}"),
                    project_key=project[0], lead=lead, spoc=spoc, stations=[])
        lines.append(line)
        for op_code, label, m_type, tech, mfr, handle in l_cfg["stations"]:
            name = f"{line['code']}-{op_code}"
            mt = MACHINE_TYPES[m_type]
            cycle = int(round(mt["cycle_s"] * _rng(f"cycle:{name}").uniform(0.9, 1.15)))
            team = SMT_TEAM if n == 4 else mt["team"]
            meta = {
                "Line": l_cfg["name"], "line": line["code"], "Technology": tech, "MachineType": m_type,
                "StationIdentifier": f"LINE-{n:02d}-{op_code}", "CycleTimeTarget": f"{cycle}s", "cycleTimeTargetS": cycle,
                "SafetyRating": mt["safety"], "PowerSupply": mt["power"],
                "ConveyorLoop": f"Conveyor Loop {line['code']}", "PalletCarrierType": "Pallet RFID High-Speed 400x400",
            }
            st = dict(name=name, id=uid(name), op=op_code, line_num=n, line=line["code"], org_id=l_cfg["org_id"],
                      line_name=l_cfg["name"], custom_identifier=f"LINE-{n:02d}-{op_code}", display_name=f"{name} – {label}",
                      label=label, machine_type=m_type, technology=tech, manufacturer=mfr, supplier=_supplier_for(mfr),
                      serial=f"SN-SYNTH-MCH-{n:02d}-{op_code}", handle=handle, team=team, cycle_time_target_s=cycle,
                      metadata=meta, owner=lead, spoc=spoc, controllers=[], components=[])
            stations.append(st)
            line["stations"].append(st)
    station_by_name = {s["name"]: s for s in stations}

    # ---------------------------------------------------------------- controllers & links
    controllers, links = [], []
    for l_cfg in LINE_CONFIGS:
        n = l_cfg["line_num"]
        lc = line_code(n)
        vlan = 100 + n
        for idx, (hn, purpose, home_op, mfr, os_ver, twincat, mes, model, st_links) in enumerate(_line_ipcs(l_cfg), start=1):
            pc = dict(hostname=hn, id=uid(hn), display=f"{hn} ({purpose})", home_station=f"{lc}-{home_op}", org_id=l_cfg["org_id"],
                      manufacturer=mfr, os=os_ver, twincat=twincat, mes=mes, model=model, line_num=n, line=lc,
                      mac=f"02:65:54:{n:02X}:{vlan:02X}:{idx:02X}", ip=f"192.168.{vlan}.{10 + idx}",
                      machine_identifier=f"HW-{hn}", vlan=vlan, vlan_name=f"VLAN_{vlan}_LINE_{n:02d}", subnet=f"192.168.{vlan}.0/24",
                      ad_ou=f"OU=AudiLine{n:02d},OU=ProductionLines,DC=factory,DC=corp", serial=f"SN-SYNTH-PC-{len(controllers) + 1:04d}",
                      stations=[])
            controllers.append(pc)
            for op, role in st_links:
                st = station_by_name[f"{lc}-{op}"]
                links.append(dict(id=uid(f"{pc['id']}:{st['id']}"), controller=hn, station=st["name"], role=role))
                st["controllers"].append((hn, role))
                pc["stations"].append((st["name"], role))
    for st in stations:  # the reporting (Primary) controller first
        st["controllers"].sort(key=lambda c: 0 if c[1] == "Primary" else 1)

    # ---------------------------------------------------------------- installed components
    components = []
    for i, st in enumerate(stations, start=1):
        for j, (part_class, slot) in enumerate(MACHINE_TYPES[st["machine_type"]]["components"], start=1):
            pcd = PART_CLASSES[part_class]
            name = f"COMP-{pcd['code']}-{st['name']}-{j}"
            comp = dict(name=name, id=uid(name), station=st["name"], part_class=part_class, slot=slot, kind=pcd["kind"],
                        display_name=f"{pcd['name']} ({st['name']})", manufacturer=pcd["mfr"], technology=pcd["tech"],
                        category=pcd["category"], tags=list(pcd["tags"]), serial=f"SN-SYNTH-CMP-{i:03d}-{j}",
                        metadata=dict(pcd["meta"], partClass=part_class, slot=slot), org_id=st["org_id"], team=st["team"],
                        storage=f"{st['name']} Mount Bay {j}", component_id=uid(f"machine-component:{st['name']}:{slot}"),
                        component_name=pcd["name"])
            components.append(comp)
            st["components"].append(comp)

    # ---------------------------------------------------------------- serialized spares (550) + bulk stock
    spares = []
    for k in range(1, 551):
        part_class = SPARE_CLASS_ORDER[(k - 1) % len(SPARE_CLASS_ORDER)]
        pcd = PART_CLASSES[part_class]
        code, title, cost = pcd["spare"]
        under_repair = k <= 50
        status = "UnderRepair" if under_repair else "InStorage"
        aisle = chr(65 + ((k - 1) % 7))
        shelf = f"Repair Depot Bay {(k % 4) + 1}" if under_repair else f"Warehouse Aisle {aisle} – Shelf {(k % 8) + 1}-{(k % 5) + 1}"
        name = f"SPARE-{code}-{k:04d}"
        spares.append(dict(name=name, id=uid(name), part_class=part_class, display_name=f"Synthetic {title} #{k:03d}",
                           manufacturer=pcd["mfr"], technology=pcd["tech"], status=status, storage=shelf, cost=cost,
                           serial=f"SN-FAKE-{pcd['mfr'][:4].upper()}-{k:04d}",
                           metadata={"partClass": part_class, "CostInHUF": cost, "Status": status, "WarehouseLocation": shelf,
                                     "InspectionDate": "2026-09-01T00:00:00Z", "CalibrationValidUntil": "2027-09-01T00:00:00Z",
                                     "AssetClass": "High-Value Serialized Spare"}))
    bulk = []
    for idx, (name, disp, mfr, tech, qty, min_qty, loc, cls) in enumerate(BULK_ITEMS, start=1):
        bulk.append(dict(name=name, id=uid(name), display_name=disp, manufacturer=mfr, technology=tech, quantity=qty, minimum=min_qty,
                         storage=loc, consumable_class=cls, serial=f"LOT-2026-AI-{idx:03d}",
                         metadata={"consumableClass": cls, "StockQuantity": qty, "MinStockThreshold": min_qty, "StorageBin": loc,
                                   "LotNumber": f"LOT-2026-AI-{idx:03d}", "UnitOfMeasure": "Units / Packs"}))

    # ---------------------------------------------------------------- machine groups
    bu_key, bu_name, bu_desc, bu_lead = BUSINESS_UNIT
    groups = [dict(key=bu_key, id=uid(f"machine-group:{bu_key}"), name=bu_name, description=bu_desc, parent=None, type="BusinessUnit",
                   color="slate", icon="Building2", lead=users[bu_lead], machines=[], machine_types=None, technology_key=None)]
    for key, name, desc, line_nums, lead in PROJECTS:
        groups.append(dict(key=key, id=uid(f"machine-group:{key}"), name=name, description=desc, parent=bu_key, type="Project",
                           color="indigo", icon="FolderKanban", lead=users[lead], machines=[], machine_types=None, technology_key=None))
    for line in lines:
        n = line["line_num"]
        groups.append(dict(key=line["group_key"], id=line["group_id"], name=line["name"],
                           description=f"Automated production line {n:02d} ({len(line['stations'])} stations)",
                           parent=line["project_key"], type="Line",
                           color="emerald" if n == 1 else "indigo" if n == 2 else "cyan" if n == 4 else "amber", icon="Factory",
                           lead=line["lead"], machines=[s["name"] for s in line["stations"]],
                           machine_types=sorted({s["machine_type"] for s in line["stations"]}), technology_key=None))
    for line in lines:
        cell = [st for st in line["stations"] if st["op"] in SUB_LINE_OPS]
        key = f"sub-{line['code'].lower()}-robot-cell"
        groups.append(dict(key=key, id=uid(f"machine-group:{key}"), name=f"{line['code']} Coordinated Robot Cell (OP080-OP090)",
                           description=f"Sub-line of {line['name']}: the two stations run by the shared robot controllers",
                           parent=line["group_key"], type="SubLine", color="orange", icon="Workflow", lead=line["lead"],
                           machines=[st["name"] for st in cell], machine_types=sorted({st["machine_type"] for st in cell}),
                           technology_key=None))
        for st in cell:
            st["sub_line_key"] = key
    for key, name, m_types, tech_key, color, icon in TECH_GROUPS:
        members = [s["name"] for s in stations if s["machine_type"] in m_types]
        groups.append(dict(key=key, id=uid(f"machine-group:{key}"), name=name,
                           description=f"Technology group: every {' / '.join(m_types)} station across the lines",
                           parent=None, type="Technological", color=color, icon=icon, lead=None, machines=members,
                           machine_types=list(m_types), technology_key=tech_key))
    group_by_key = {g["key"]: g for g in groups}
    memberships = [dict(id=uid(f"mgm:{g['key']}:{name}"), group=g["key"], station=name)
                   for g in groups if g["type"] in ("Line", "SubLine", "Technological") for name in g["machines"]]

    # ---------------------------------------------------------------- people: skills, crews, dispatch
    skills = [dict(code=c, id=uid(f"skill:{c}"), name=n, description=d, category=cat, issuer=iss, validity_months=vm)
              for c, n, d, cat, iss, vm in SKILLS]
    personnel_groups, personnel_skills = [], []
    for key, name, desc, discipline, shift, member_ids, skill in PERSONNEL_GROUPS:
        personnel_groups.append(dict(key=key, id=uid(f"personnel-group:{key}"), name=name, description=desc, discipline=discipline,
                                     shift_pattern=shift, members=list(member_ids), skill=skill))
        for m in member_ids:
            if skill:
                personnel_skills.append(dict(id=uid(f"personnel-skill:{m}:{skill}"), user=m, skill=skill,
                                             certificate=f"SYN-CERT-{m[-2:]}-{skill.split('-')[1]}"))
    recipes = []
    for key, name, desc, discipline, tech, crew, on_call, skill, targets_lines in DELEGATION_RECIPES:
        crew_members = next(g for g in personnel_groups if g["key"] == crew)["members"]
        recipes.append(dict(key=key, id=uid(f"delegation-recipe:{key}"), name=name, description=desc, discipline=discipline,
                            technology=tech, crew=crew, primary_user=crew_members[0], on_call=on_call, skill=skill,
                            escalation=ESCALATION_GROUP, escalation_timeout_minutes=30,
                            machine_groups=[l["group_key"] for l in lines] if targets_lines else []))
    # The controls recipe is additionally bound to the multi-IPC laser cells (OP060).
    bindings = [dict(id=uid(f"binding:dr-controls:{s['name']}"), station=s["name"], recipe="dr-controls", discipline="Controls")
                for s in stations if s["op"] == "OP060"]
    technician_rules = [
        dict(key="rule-controls-l1", id=uid("technician-rule:rule-controls-l1"), name="Line 01 Controls Specialist Rule",
             technician="usr-synth-01", scope_type="group", target_group="grp-line-01", target=None, category="PLC",
             backup="usr-synth-02", assigned_by_role="manager"),
        dict(key="rule-vision-l4", id=uid("technician-rule:rule-vision-l4"), name="Line 04 SMT AOI Vision Rule",
             technician="usr-synth-04", scope_type="technology", target_group=None, target="SMT", category="Camera",
             backup="usr-synth-16", assigned_by_role="group_leader"),
    ]
    # Relative to load time (hours): one technician is away right now, one next week.
    absences = [
        dict(key="abs-profibus-training", id=uid("absence:abs-profibus-training"), technician="usr-synth-42", reason="Training",
             start_h=-4, end_h=28, marked_by="usr-synth-09", backup="usr-synth-37"),
        dict(key="abs-torquewrench-vacation", id=uid("absence:abs-torquewrench-vacation"), technician="usr-synth-51", reason="Vacation",
             start_h=120, end_h=288, marked_by="usr-synth-09", backup="usr-synth-31"),
    ]

    return dict(users=users, org_ids=org_ids, lines=lines, stations=stations, controllers=controllers, links=links,
                components=components, spares=spares, bulk=bulk, groups=groups, group_by_key=group_by_key,
                memberships=memberships, skills=skills, personnel_groups=personnel_groups, personnel_skills=personnel_skills,
                recipes=recipes, bindings=bindings, technician_rules=technician_rules, absences=absences)


# ======================================================================================
# Rendering: inventory CSV
# ======================================================================================
def csv_rows(model):
    def row(**kw):
        base = {k: "" for k in CSV_FIELDS}
        base["IsStockItem"] = "False"
        base.update(kw)
        return base

    rows = []
    for st in model["stations"]:
        rows.append(row(Type="Machine", Name=st["name"], DisplayName=st["display_name"], ResponsibleTeam=st["team"],
                        Manufacturer=st["manufacturer"], Supplier=st["supplier"], SerialNumber=st["serial"],
                        StationIdentifier=st["custom_identifier"], Metadata=json.dumps(st["metadata"]),
                        PinnedObjectHandle=st["handle"], OrganizationId=st["org_id"],
                        StorageLocation=f"{st['line_name']} – Cell {st['op']}", EquipmentStatus="InMachine",
                        Technology=st["technology"], MachineType=st["machine_type"], GroupId=st["line_name"]))
    station_by_name = {s["name"]: s for s in model["stations"]}
    for pc in model["controllers"]:
        meta = {
            "IPAddress": pc["ip"], "MACAddress": pc["mac"], "MachineIdentifier": pc["machine_identifier"], "VlanId": pc["vlan"],
            "VlanName": pc["vlan_name"], "Subnet": pc["subnet"], "AdOuPath": pc["ad_ou"], "OperatingSystem": pc["os"],
            "OsVersion": pc["os"], "Model": pc["model"], "TwinCATProject": pc["twincat"], "MESClient": pc["mes"],
            "BeckhoffRtDriver": "TcRTEthernet active (Port 851 bound)",
            "ControlledStations": [{"Station": s, "Role": r} for s, r in pc["stations"]],
            "Disks": {"C:": {"Caption": "C: (OS)", "TotalFreeGB": 142.5, "SizeGB": 256.0},
                      "D:": {"Caption": "D: (Telemetry Spool)", "TotalFreeGB": 680.0, "SizeGB": 1000.0}},
            "RAM": "32 GB DDR4-3200 Industrial ECC",
            "CPU": "Intel Core i7-1185GRE @ 2.80GHz (4 Cores / 8 Threads)",
        }
        rows.append(row(Type="ClientPc", Name=pc["hostname"], DisplayName=pc["display"],
                        ResponsibleTeam="Controls Engineering (Synthetic AI Guild);Platform Operations (Synthetic AI Guild)",
                        Manufacturer=pc["manufacturer"], Supplier="Direct Automation Europe", SerialNumber=pc["serial"],
                        StationIdentifier=pc["home_station"], ClientPcHostname=pc["hostname"], Metadata=json.dumps(meta),
                        PinnedObjectHandle=station_by_name[pc["home_station"]]["handle"], OrganizationId=pc["org_id"],
                        StorageLocation=f"Control Cabinet {pc['home_station']}", EquipmentStatus="InMachine",
                        Technology="Controls & Automation"))
    for c in model["components"]:
        rows.append(row(Type=c["kind"], Name=c["name"], DisplayName=c["display_name"], ResponsibleTeam=c["team"],
                        Manufacturer=c["manufacturer"], Supplier="Direct Automation Europe", SerialNumber=c["serial"],
                        ParentName=c["station"], StationIdentifier=c["station"], Metadata=json.dumps(c["metadata"]),
                        OrganizationId=c["org_id"], StorageLocation=c["storage"], EquipmentStatus="InMachine",
                        MachineId=c["station"], Technology=c["technology"]))
    for s in model["spares"]:
        rows.append(row(Type="HardwareComponent", Name=s["name"], DisplayName=s["display_name"],
                        ResponsibleTeam="Plant Maintenance (Synthetic AI Guild)", Manufacturer=s["manufacturer"],
                        Supplier="Direct Automation Europe", SerialNumber=s["serial"], Metadata=json.dumps(s["metadata"]),
                        OrganizationId="org-maintenance", StorageLocation=s["storage"], EquipmentStatus=s["status"],
                        Technology=s["technology"]))
    for b in model["bulk"]:
        rows.append(row(Type="HardwareComponent", Name=b["name"], DisplayName=b["display_name"],
                        ResponsibleTeam="Plant Maintenance (Synthetic AI Guild)", Manufacturer=b["manufacturer"],
                        Supplier="RS Components", SerialNumber=b["serial"], Metadata=json.dumps(b["metadata"]),
                        OrganizationId="org-maintenance", StorageLocation=b["storage"], EquipmentStatus="InStorage",
                        StockQuantity=str(b["quantity"]), MinStockThreshold=str(b["minimum"]), IsStockItem="True",
                        Technology=b["technology"]))
    return rows


def render_csv(model):
    buf = io.StringIO(newline="")
    writer = csv.DictWriter(buf, fieldnames=CSV_FIELDS)
    writer.writeheader()
    writer.writerows(csv_rows(model))
    return buf.getvalue()


# ======================================================================================
# Rendering: production topology (read by the fleet simulator and the frontend tests)
# ======================================================================================
def render_topology(model):
    pcs = {p["hostname"]: p for p in model["controllers"]}
    out_lines = []
    for line in model["lines"]:
        sts = []
        for st in line["stations"]:
            sts.append({
                "id": st["name"], "name": st["display_name"], "customIdentifier": st["custom_identifier"],
                "type": st["machine_type"], "technology": st["technology"], "team": st["team"],
                "pinnedObjectHandle": st["handle"], "cycleTimeTargetS": st["cycle_time_target_s"],
                "components": [{"slot": c["slot"], "partClass": c["part_class"], "name": c["component_name"],
                                "category": c["category"], "serialNumber": c["serial"]} for c in st["components"]],
                "pcs": [{"id": hn, "hostname": hn, "role": role, "machineIdentifier": pcs[hn]["machine_identifier"],
                         "ip": pcs[hn]["ip"], "os": pcs[hn]["os"], "status": "Healthy",
                         "twincat": pcs[hn]["twincat"], "mes": pcs[hn]["mes"]} for hn, role in st["controllers"]],
            })
        out_lines.append({"id": line["org_id"], "code": line["code"], "line_num": line["line_num"], "name": line["name"],
                          "tech": line["tech"], "stations": sts})
    topology = {"production_hall": {"name": "Heimdall Smart Factory Giga-01 (Synthetic AI Facility)", "lines": out_lines}}
    return json.dumps(topology, indent=2, ensure_ascii=False) + "\n"


# ======================================================================================
# Rendering: directory / identity fixture
# ======================================================================================
ORG_TO_SG = {
    "org-line-01": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-02": "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-03": "CN=SG-Line03-Powertrain,OU=AudiLine03,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-04": "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-05": "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-06": "CN=SG-Line06-PressFit,OU=AudiLine06,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-07": "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp",
    "org-line-08": "CN=SG-Line08-Integration,OU=AudiLine08,OU=ProductionLines,DC=factory,DC=corp",
    "org-controls": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
    "org-robotics": "CN=SG-Line05-Welding,OU=AudiLine05,OU=ProductionLines,DC=factory,DC=corp",
    "org-vision": "CN=SG-Line07-Metrology,OU=AudiLine07,OU=ProductionLines,DC=factory,DC=corp",
    "org-smt": "CN=SG-Line04-SMT,OU=AudiLine04,OU=ProductionLines,DC=factory,DC=corp",
    "org-assembly": "CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp",
    "org-maintenance": "CN=SG-Plant-Maintenance,OU=Maintenance,OU=EngineeringDisciplines,DC=factory,DC=corp",
    "org-platform": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
    "org-audi-proj": "CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp",
}
ADMIN_SG = "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c"

FIXTURE_SECURITY_GROUPS = [
    ("sg-entra-admins", ADMIN_SG, "OT Plant Administrators (Synthetic Superusers)", "EntraID", "system_admin",
     "Platform Operations (Synthetic AI Guild)", "owner", ["usr-synth-03", "usr-synth-60"]),
    ("sg-line01-controls", ORG_TO_SG["org-line-01"], "Line 01 Controls Engineers", "ActiveDirectory", "controls_engineer",
     "Line 01 – Synthetic Audi E-Tron Module Line", "admin", ["usr-synth-01", "usr-synth-02"]),
    ("sg-line02-assembly", ORG_TO_SG["org-line-02"], "Line 02 Assembly Techs", "ActiveDirectory", "technician",
     "Line 02 – Synthetic Audi Battery Pack Line", "member", ["usr-synth-11", "usr-synth-28", "usr-synth-39"]),
    ("sg-line03-powertrain", ORG_TO_SG["org-line-03"], "Line 03 Powertrain Specialists", "ActiveDirectory", "engineer",
     "Line 03 – Synthetic Audi Powertrain Line", "admin", ["usr-synth-07", "usr-synth-23"]),
    ("sg-line04-smt", ORG_TO_SG["org-line-04"], "Line 04 SMT Placement Leads", "ActiveDirectory", "lead_engineer",
     "Line 04 – Synthetic Electronics SMT Placement", "admin", ["usr-synth-04", "usr-synth-17", "usr-synth-21"]),
    ("sg-line05-welding", ORG_TO_SG["org-line-05"], "Line 05 Robotic Welders", "ActiveDirectory", "engineer",
     "Line 05 – Synthetic Body-in-White Robotic Welding", "admin", ["usr-synth-09", "usr-synth-10", "usr-synth-22", "usr-synth-33"]),
    ("sg-line06-pressfit", ORG_TO_SG["org-line-06"], "Line 06 Press Fit Engineers", "ActiveDirectory", "engineer",
     "Line 06 – Synthetic Precision Press Fit", "admin", ["usr-synth-15", "usr-synth-25"]),
    ("sg-line07-metrology", ORG_TO_SG["org-line-07"], "Line 07 Quality Metrologists", "ActiveDirectory", "lead_engineer",
     "Line 07 – Synthetic Optical Quality Metrology", "admin", ["usr-synth-05", "usr-synth-13", "usr-synth-30", "usr-synth-53"]),
    ("sg-line08-eol", ORG_TO_SG["org-line-08"], "Line 08 Vehicle Integration Leads", "ActiveDirectory", "lead_engineer",
     "Line 08 – Synthetic End-of-Line Vehicle Integration", "admin", ["usr-synth-14", "usr-synth-31"]),
    ("sg-plant-maint", ORG_TO_SG["org-maintenance"], "Plant Maintenance Specialists", "ActiveDirectory", "technician",
     "Plant Maintenance (Synthetic AI Guild)", "member", ["usr-synth-15", "usr-synth-27", "usr-synth-32", "usr-synth-57"]),
]

FIXTURE_IPC_MODELS = {
    "OP030-DEDICATED": ("Beckhoff C6030 Fanless Edge", "1-1 Dedicated Station Controller"),
    "CONVEYOR-MAIN": ("Siemens SIMATIC IPC477E Industrial", "1-n Line Master PLC (conveyor and standard stations)"),
    "ROB-ALPHA": ("Advantech UNO-2484G Industrial PC", "m-n Distributed Robotics Controller Alpha"),
    "ROB-BETA": ("Advantech UNO-2484G Industrial PC", "m-n Distributed Robotics Controller Beta"),
    "OP060-PLC": ("Beckhoff C6032 High-Performance IPC", "n-1 Motion & Safety PLC Core"),
    "OP060-VISION": ("Dell OptiPlex 7090 Micro Edge", "n-1 Real-Time Vision Inspector"),
    "OP060-MES-GATE": ("Advantech UNO-2271G Edge Gateway", "n-1 Plant MES Gateway Connector"),
}

DISCIPLINE_OUS = [
    {"ouPath": "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp", "name": "Robotics", "vlanId": 10, "vlanName": "VLAN 10 - Production Line",
     "subnet": "10.10.10.0/24", "location": "Line 06 - Hall A", "purpose": "Robotic Pick & Place / Handling", "machineType": "Manipulator",
     "hostCount": 2, "candidateHostnames": ["IPC-L06-ROB-ALPHA", "IPC-L06-ROB-BETA"]},
    {"ouPath": "OU=VisionInspection,OU=VLAN20-Quality,DC=factory,DC=corp", "name": "VisionInspection", "vlanId": 20,
     "vlanName": "VLAN 20 - Optical Quality Inspection", "subnet": "10.10.20.0/24", "location": "Line 07 - Hall C",
     "purpose": "AOI Defect Classification & Inspection", "machineType": "Automatic Optical Inspection", "hostCount": 2,
     "candidateHostnames": ["IPC-L07-OP060-VISION", "IPC-L04-OP060-VISION"]},
    {"ouPath": "OU=MillingMachining,OU=VLAN30-Machining,DC=factory,DC=corp", "name": "MillingMachining", "vlanId": 30,
     "vlanName": "VLAN 30 - CNC Milling & Machining", "subnet": "10.10.30.0/24", "location": "Line 03 - Hall A",
     "purpose": "Precision CNC Milling & Spindle Control", "machineType": "CNC Machine", "hostCount": 1,
     "candidateHostnames": ["IPC-L03-OP030-DEDICATED"]},
    {"ouPath": "OU=TestingValidation,OU=VLAN40-Testing,DC=factory,DC=corp", "name": "TestingValidation", "vlanId": 40,
     "vlanName": "VLAN 40 - End of Line Electrical Testing", "subnet": "10.10.40.0/24", "location": "Line 08 - Hall D",
     "purpose": "High-Voltage Isolation & Leak Testing", "machineType": "EOL Tester", "hostCount": 1,
     "candidateHostnames": ["IPC-L08-OP030-DEDICATED"]},
    {"ouPath": "OU=JoiningWelding,OU=VLAN50-Welding,DC=factory,DC=corp", "name": "Joining", "vlanId": 50,
     "vlanName": "VLAN 50 - Joining & Laser Seam", "subnet": "10.10.50.0/24", "location": "Line 05 - Hall B",
     "purpose": "Laser & Ultrasonic Joining", "machineType": "LaserWelder", "hostCount": 2,
     "candidateHostnames": ["IPC-L05-OP060-PLC", "IPC-L05-OP030-DEDICATED"]},
    {"ouPath": "OU=ConveyorTransport,OU=VLAN60-Conveyors,DC=factory,DC=corp", "name": "ConveyorTransport", "vlanId": 60,
     "vlanName": "VLAN 60 - Pallet Transfer Conveyors", "subnet": "10.10.60.0/24", "location": "Central Logistics Spine",
     "purpose": "Main Loop Conveyor Transport & Pallet RFID", "machineType": "ConveyorPLC", "hostCount": 2,
     "candidateHostnames": ["IPC-L01-CONVEYOR-MAIN", "IPC-L02-CONVEYOR-MAIN"]},
]


def build_fixture(model):
    users = model["users"]
    fx_users = []
    for u in users.values():
        if u["kind"] != "persona":
            continue
        sg_ids = [ORG_TO_SG[o] for o in u["orgs"] if o in ORG_TO_SG]
        if u["role"] in ("system_admin", "plant_director"):
            sg_ids.append(ADMIN_SG)
        if not sg_ids:
            sg_ids.append(ORG_TO_SG["org-line-01"])
        fx_users.append({
            "id": u["id"], "name": u["name"], "email": u["email"], "username": u["username"], "primaryRole": u["role"],
            "jobTitle": u["title"], "department": u["department"], "securityGroupIds": sorted(set(sg_ids)),
            "presence": {"availability": "Busy" if u["id"] in ("usr-synth-08", "usr-synth-18") else "Available", "isOutOfOffice": False},
            "mfaPolicy": "always" if u["role"] in ("system_admin", "plant_director") else "weekly",
        })

    client_pcs = []
    for pc in model["controllers"]:
        suffix = pc["hostname"].split("-", 2)[2]
        hw_model, purpose = FIXTURE_IPC_MODELS[suffix]
        n = pc["line_num"]
        client_pcs.append({
            "hostname": pc["hostname"], "name": f"{pc['hostname']} ({purpose})", "machineIdentifier": pc["machine_identifier"],
            "ipAddress": pc["ip"], "macAddress": pc["mac"], "vlanId": pc["vlan"], "adOuPath": pc["ad_ou"],
            "machineType": "IndustrialController", "groupId": next(l["name"] for l in model["lines"] if l["line_num"] == n),
            "osVersion": pc["os"],
            "controlledStations": [{"stationId": s, "role": r} for s, r in pc["stations"]],
            "cmiHardware": {
                "cpu": {"Name": "Intel(R) Core(TM) i7-1185GRE @ 2.80GHz", "NumberOfCores": 8, "NumberOfLogicalProcessors": 16, "MaxClockSpeed": 2800},
                "memory": {"Capacity": 34359738368, "Speed": 3200, "Manufacturer": "Innodisk Industrial", "PartNumber": "M4D0-BGS2QC0K"},
                "disks": [{"Caption": "C:", "Size": 274877906944, "FreeSpace": 150323855360, "FileSystem": "NTFS"},
                          {"Caption": "D:", "Size": 1099511627776, "FreeSpace": 730144440320, "FileSystem": "NTFS"}],
                "bios": {"Manufacturer": "Beckhoff Automation GmbH", "SMBIOSBIOSVersion": f"CB3063-{n:02d}-V1.4", "SerialNumber": f"BIOS-{pc['hostname']}"},
                "computerSystem": {"Name": pc["hostname"], "Manufacturer": hw_model.split()[0], "Model": hw_model,
                                   "TotalPhysicalMemory": 34359738368, "Domain": PLANT_DOMAIN},
                "network": {"Description": "Intel(R) I210 Gigabit Network Connection", "IPAddress": pc["ip"], "MACAddress": pc["mac"],
                            "DefaultIPGateway": f"192.168.{pc['vlan']}.1", "DNSServerSearchOrder": [f"192.168.{pc['vlan']}.1", "10.0.0.53"]},
                "os": {"Caption": f"Microsoft {pc['os']}", "Version": "10.0.19045", "BuildNumber": "19045", "OSArchitecture": "64-bit",
                       "InstallDate": "2026-09-01T00:00:00Z"},
            },
            "installedPackages": [
                "Beckhoff TwinCAT 3.1 Build 4026.10 (Port 851)", "TcRTEthernet Real-Time Driver v3.1", "Audi MES Production Client v3.1",
                "Siemens Opcenter MES Connector v4.2", "Cognex In-Sight Explorer 6.5.0", "Wireshark 4.2.4",
                "Node-RED Industrial Telemetry Daemon",
            ],
        })

    ad_ous = []
    for line in model["lines"]:
        n = line["line_num"]
        hosts = [p["hostname"] for p in model["controllers"] if p["line_num"] == n]
        ad_ous.append({
            "ouPath": f"OU=AudiLine{n:02d},OU=ProductionLines,DC=factory,DC=corp", "name": f"Audi Production Line {n:02d} (OT Subnet)",
            "vlanId": 100 + n, "vlanName": f"VLAN_{100 + n}_LINE_{n:02d}", "subnet": f"192.168.{100 + n}.0/24",
            "location": f"Production Corridor Line {n:02d}", "purpose": "Industrial Automation Pallet Conveyors & Cells",
            "machineType": "IndustrialController", "hostCount": len(hosts), "candidateHostnames": hosts,
        })
    ad_ous.extend(DISCIPLINE_OUS)

    machine_groups = []
    for line in model["lines"]:
        g = model["group_by_key"][line["group_key"]]
        machine_groups.append({"id": g["key"], "name": g["name"], "description": g["description"], "parentId": None,
                               "machineIds": list(g["machines"]), "machineTypes": list(g["machine_types"]), "color": g["color"]})

    def user_name(u_id):
        return users[u_id]["name"]

    tech_rules = []
    for r in model["technician_rules"]:
        tech_rules.append({
            "id": r["key"], "name": r["name"], "technicianId": r["technician"], "technicianName": user_name(r["technician"]),
            "technicianEmail": users[r["technician"]]["email"], "scopeType": r["scope_type"],
            "targetId": r["target_group"] or r["target"], "categoryFilter": r["category"],
            "backupTechnicianId": r["backup"], "backupTechnicianName": user_name(r["backup"]), "assignedByRole": r["assigned_by_role"],
        })

    spare_summary = {}
    for s in model["spares"]:
        e = spare_summary.setdefault(s["part_class"], {"partClass": s["part_class"], "inStorage": 0, "underRepair": 0,
                                                       "manufacturer": s["manufacturer"], "names": []})
        e["inStorage" if s["status"] == "InStorage" else "underRepair"] += 1
        e["names"].append(s["name"])

    dataset = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "metadata": {
            "version": "3.0.0", "plantName": PLANT_NAME, "plantCode": PLANT_CODE, "domain": PLANT_DOMAIN,
            "entraTenantId": "00000000-de70-4e1d-8000-00000000c0de", "createdAt": "2026-09-12T00:00:00Z",
            "description": "Canonical enterprise dataset: 100 automated stations across 8 lines, 56 IPC controllers with TwinCAT & MES, "
                           "60 fake AI-generated users. GENERATED by seed_data/seed_pipeline.py --generate-all; do not edit by hand.",
        },
        "organizations": [{"id": o["id"], "name": o["name"], "slug": o["slug"], "description": o["desc"]} for o in ORGANIZATIONS],
        "securityGroups": [{"id": gid, "groupIdentifier": ident, "displayName": disp, "identityProvider": idp, "mappedRole": role,
                            "mappedOrganizationName": org_name, "mappedOrgRole": org_role, "autoCreateOrg": True, "isEnabled": True,
                            "memberUserIds": members}
                           for gid, ident, disp, idp, role, org_name, org_role, members in FIXTURE_SECURITY_GROUPS],
        "users": fx_users,
        "activeDirectoryOUs": ad_ous,
        "clientPcs": client_pcs,
        "machineGroups": machine_groups,
        "technicianRules": tech_rules,
        "shiftAbsences": [],
        "mfaPolicies": {"system_admin": {"threshold": "always"}, "engineer": {"threshold": "weekly"}, "technician": {"threshold": "monthly"}},
        # ---- sections below are read by the fleet simulator (crews, stock); the BFF ignores them ----
        "personnelGroups": [{"id": g["key"], "name": g["name"], "discipline": g["discipline"], "shiftPattern": g["shift_pattern"],
                             "skill": g["skill"], "members": [{"userId": m, "name": user_name(m), "role": users[m]["role"]} for m in g["members"]]}
                            for g in model["personnel_groups"]],
        "delegationRecipes": [{"id": r["key"], "name": r["name"], "discipline": r["discipline"], "technology": r["technology"],
                               "personnelGroupId": r["crew"], "primaryUserId": r["primary_user"], "onCallUserId": r["on_call"],
                               "escalationGroupId": r["escalation"], "escalationTimeoutMinutes": r["escalation_timeout_minutes"]}
                              for r in model["recipes"]],
        "plannedAbsences": [{"userId": a["technician"], "name": user_name(a["technician"]), "reason": a["reason"],
                             "startOffsetHours": a["start_h"], "endOffsetHours": a["end_h"], "backupUserId": a["backup"]}
                            for a in model["absences"]],
        "spareParts": sorted(({k: v for k, v in e.items() if k != "names"} | {"firstName": e["names"][0]}
                              for e in spare_summary.values()), key=lambda e: e["partClass"]),
        "consumables": [{"name": b["name"], "consumableClass": b["consumable_class"], "quantity": b["quantity"], "minimum": b["minimum"]}
                        for b in model["bulk"]],
    }
    return dataset


def render_fixture(model):
    return json.dumps(build_fixture(model), indent=2, ensure_ascii=False) + "\n"


# ======================================================================================
# Rendering: transactional SQL reseed
# ======================================================================================
def q(value):
    """SQL literal."""
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return "'" + str(value).replace("'", "''") + "'"


def qj(obj):
    return q(json.dumps(obj, ensure_ascii=False)) + "::jsonb"


def qjt(obj):
    """JSON stored in a text column."""
    return q(json.dumps(obj, ensure_ascii=False, separators=(",", ":")))


def qarr(values):
    return "ARRAY[" + ", ".join(q(v) for v in values) + "]::text[]" if values else "ARRAY[]::text[]"


TRUNCATE_TABLES = [
    'backend.maintenance_tickets', 'backend.ticket_comments', 'backend.ticket_attachments', 'backend.machine_service_logs',
    'backend.component_part_histories', 'backend.machine_components', 'backend.machine_delegation_bindings',
    'backend.delegation_machine_groups', 'backend.delegation_maintenance_groups', 'backend.delegation_recipes',
    'backend.personnel_group_members', 'backend.personnel_skills', 'backend.personnel_groups', 'backend.skill_definitions',
    'backend.technician_rules', 'backend.shift_absences', 'backend.machine_group_memberships',
    'backend."StationControllers"', 'backend."ItemResponsibilities"', 'backend."PcResponsibilities"',
    'backend.equipment_interconnects', 'backend.agent_events', 'backend.queued_agent_commands', 'backend.diagnostic_snapshots',
    'backend.inventory_items', 'backend.stations', 'backend.client_pcs', 'backend.machine_groups',
    'backend.manufacturers', 'backend.suppliers', 'backend.responsible_teams',
    'backend.security_group_mappings', 'backend.ad_ou_governances', 'backend.system_settings', 'backend.client_certificates',
    'backend.schema_version_manifest', 'backend.audit_logs', 'backend.malformed_telemetry_quarantine',
]


def render_sql(model):
    users, groups = model["users"], model["group_by_key"]
    station_by_name = {s["name"]: s for s in model["stations"]}
    pc_by_host = {p["hostname"]: p for p in model["controllers"]}
    pg_by_key = {g["key"]: g for g in model["personnel_groups"]}
    skill_by_code = {s["code"]: s for s in model["skills"]}
    recipe_by_key = {r["key"]: r for r in model["recipes"]}
    sql = []
    add = sql.append

    add("-- Heimdall Enterprise Plant Seed SQL (Giga-01: 100 machines, 8 lines, 56 IPCs, 550 serialized spares, 107 users)")
    add("-- GENERATED by seed_data/seed_pipeline.py --generate-all. Reference / master data only: tickets, telemetry and")
    add("-- stock movements are produced by the fleet simulator (see docs/guide/DEV_GUIDE.md).")
    add("SET statement_timeout = 0;")
    add("BEGIN;")
    add("-- Safety guard: this script wipes every domain table and seeds a well-known dev admin credential.")
    add("-- Only allow it on local dev/test/staging databases.")
    add("DO $$ BEGIN")
    add("  IF current_database() !~* '(dev|test|staging|local)' THEN")
    add("    RAISE EXCEPTION 'incremental_seed.sql refused: database % is not a dev/test/staging database', current_database();")
    add("  END IF;")
    add("END $$;")
    add("SET search_path TO backend, auth, public;")
    add("")
    add("-- Remove all previous plant data (tables that do not exist in an older schema are skipped).")
    add("DO $$ DECLARE t text; BEGIN")
    add("  FOREACH t IN ARRAY ARRAY[" + ", ".join(q(t) for t in TRUNCATE_TABLES) + "] LOOP")
    add("    IF to_regclass(t) IS NOT NULL THEN EXECUTE 'TRUNCATE TABLE ' || t || ' CASCADE'; END IF;")
    add("  END LOOP;")
    add("END $$;")
    add("-- Rows the Nordvik demo loader owns outside the truncated tables.")
    add("DO $$ BEGIN")
    add("  IF to_regclass('backend.lua_scripts') IS NOT NULL THEN DELETE FROM backend.lua_scripts WHERE created_by = 'demo-seed'; END IF;")
    add("  IF to_regclass('backend.generic_templates') IS NOT NULL THEN DELETE FROM backend.generic_templates WHERE organization_id = 'demo-org-nordvik'; END IF;")
    add("END $$;")
    add("")
    add("-- Identity: everything is replaced except the dev admin and its sessions (the browser stays logged in).")
    add(f"DELETE FROM auth.session WHERE user_id <> {q(ADMIN_USER['id'])};")
    add("DELETE FROM auth.invitation;")
    add("DELETE FROM auth.member;")
    add(f"DELETE FROM auth.\"user\" WHERE id <> {q(ADMIN_USER['id'])};")
    add("DELETE FROM auth.organization;")
    add("")
    add(f"-- {len(ORGANIZATIONS)} organizations")
    for org in ORGANIZATIONS:
        add(f"INSERT INTO auth.organization (id, name, slug, created_at) VALUES ({q(org['id'])}, {q(org['name'])}, {q(org['slug'])}, NOW()) ON CONFLICT (id) DO NOTHING;")

    add("\n-- Primary system administrator (dev admin account contract)")
    a = ADMIN_USER
    add("INSERT INTO auth.\"user\" (id, name, email, email_verified, role, username, created_at, updated_at) "
        f"VALUES ({q(a['id'])}, {q(a['name'])}, {q(a['email'])}, true, {q(a['role'])}, {q(a['username'])}, NOW(), NOW()) "
        "ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, email = EXCLUDED.email, role = EXCLUDED.role, username = EXCLUDED.username;")
    add("INSERT INTO auth.account (id, account_id, provider_id, user_id, password, issuer, created_at, updated_at) "
        f"VALUES ('acc-admin-primary', {q(a['id'])}, 'credential', {q(a['id'])}, {q(ADMIN_PASSWORD_HASH)}, 'local:credential', NOW(), NOW()) "
        "ON CONFLICT (id) DO UPDATE SET password = EXCLUDED.password, issuer = EXCLUDED.issuer;")
    add(f"INSERT INTO auth.member (id, organization_id, user_id, role, created_at) VALUES ('mem-admin-platform', 'org-platform', {q(a['id'])}, 'owner', NOW()) ON CONFLICT (id) DO NOTHING;")
    add(f"INSERT INTO auth.member (id, organization_id, user_id, role, created_at) VALUES ('mem-admin-controls', 'org-controls', {q(a['id'])}, 'admin', NOW()) ON CONFLICT (id) DO NOTHING;")

    add(f"\n-- {len(users)} users: 60 synthetic plant personas + the system / persona catalogue of the web app")
    for u in users.values():
        add("INSERT INTO auth.\"user\" (id, name, email, email_verified, role, username, created_at, updated_at) "
            f"VALUES ({q(u['id'])}, {q(u['name'])}, {q(u['email'])}, true, {q(u['role'])}, {q(u['username'])}, NOW(), NOW()) "
            "ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, email = EXCLUDED.email, role = EXCLUDED.role, username = EXCLUDED.username;")
        for org_id in u["orgs"]:
            add(f"INSERT INTO auth.member (id, organization_id, user_id, role, created_at) VALUES ({q(uid(u['id'] + ':' + org_id))}, {q(org_id)}, {q(u['id'])}, {q(member_role(u['role']))}, NOW()) ON CONFLICT (id) DO NOTHING;")

    add("\n-- Reference tables (manufacturers, suppliers, responsible teams)")
    rows = csv_rows(model)
    for m in sorted({r["Manufacturer"] for r in rows if r["Manufacturer"]}):
        add(f"INSERT INTO backend.manufacturers (id, name) VALUES ({q(uid(m))}, {q(m)}) ON CONFLICT (name) DO NOTHING;")
    for s in sorted({r["Supplier"] for r in rows if r["Supplier"]}):
        add(f"INSERT INTO backend.suppliers (id, name) VALUES ({q(uid(s))}, {q(s)}) ON CONFLICT (name) DO NOTHING;")
    teams = sorted({t.strip() for r in rows if r["ResponsibleTeam"] for t in r["ResponsibleTeam"].split(";")})
    for t in teams:
        add(f"INSERT INTO backend.responsible_teams (id, name) VALUES ({q(uid(t))}, {q(t)}) ON CONFLICT (name) DO NOTHING;")

    add("\n-- Machine groups: business unit -> projects -> lines, plus technology fleets")
    for g in model["groups"]:
        parent = q(groups[g["parent"]]["id"]) if g["parent"] else "NULL"
        machine_ids = [station_by_name[n]["id"] for n in g["machines"]]
        lead = g["lead"]
        add("INSERT INTO backend.machine_groups (id, name, description, parent_id, color, icon, lead_engineer_id, lead_engineer_name, "
            "machine_ids_json, machine_types_json, created_at, updated_at, group_type, interface_contract, technology_key) "
            f"VALUES ({q(g['id'])}, {q(g['name'])}, {q(g['description'])}, {parent}, {q(g['color'])}, {q(g['icon'])}, "
            f"{q(lead['id'] if lead else None)}, {q(lead['name'] if lead else None)}, {qjt(machine_ids)}, "
            f"{qjt(g['machine_types']) if g['machine_types'] else 'NULL'}, NOW(), NOW(), {q(g['type'])}, NULL, {q(g['technology_key'])});")

    add("\n-- Edge IPCs (controllers)")
    for pc in model["controllers"]:
        csv_row = next(r for r in rows if r["Type"] == "ClientPc" and r["Name"] == pc["hostname"])
        meta = json.loads(csv_row["Metadata"])
        # Dev dataset: every simulated IPC is linked to the one local remote-desktop gateway
        # (the Windows dev container's noVNC port). The UI resolves this link per controller.
        meta.setdefault("RemoteAccess", DEV_REMOTE_ACCESS_LINK)
        add("INSERT INTO backend.client_pcs (id, name, mac_address, hostname, machine_identifier, ip_address, vlan_id, vlan_name, subnet, "
            "ad_ou_path, system_metadata, pinned_object_handle, organization_id) "
            f"VALUES ({q(pc['id'])}, {q(pc['hostname'])}, {q(pc['mac'])}, {q(pc['hostname'])}, {q(pc['machine_identifier'])}, {q(pc['ip'])}, "
            f"{pc['vlan']}, {q(pc['vlan_name'])}, {q(pc['subnet'])}, {q(pc['ad_ou'])}, {qj(meta)}, {q(csv_row['PinnedObjectHandle'])}, {q(pc['org_id'])});")

    add("\n-- 100 machines (stations) across 8 lines")
    bu_id = groups[BUSINESS_UNIT[0]]["id"]
    for st in model["stations"]:
        line = next(l for l in model["lines"] if l["line_num"] == st["line_num"])
        # Dev dataset: every station is linked to the sample digital-twin model; the UI resolves
        # the model through the station (metadata.model3dUrl), it has no default of its own.
        meta = dict(st["metadata"])
        meta.setdefault("model3dUrl", DEV_STATION_MODEL_URL)
        add("INSERT INTO backend.inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id, "
            "storage_location, equipment_status, is_stock_item, technology) "
            f"VALUES ({q(st['id'])}, {q(st['name'])}, {q(st['display_name'])}, {q(uid(st['manufacturer']))}, {q(uid(st['supplier']))}, {qj(meta)}, "
            f"{q(st['serial'])}, {q(st['org_id'])}, {q(st['line_name'] + ' – Cell ' + st['op'])}, 'InMachine', false, {q(st['technology'])});")
        owner, spoc = st["owner"], st["spoc"]
        add("INSERT INTO backend.stations (id, custom_identifier, pinned_object_handle, machine_type, group_id, status, technologies, "
            "line_group_id, sub_line_group_id, project_group_id, business_unit_group_id, owner, owner_user_id, spoc, spoc_user_id) "
            f"VALUES ({q(st['id'])}, {q(st['custom_identifier'])}, {q(st['handle'])}, {q(st['machine_type'])}, {q(st['line_name'])}, 'active', "
            f"{qarr([st['technology']])}, {q(line['group_id'])}, {q(groups[st['sub_line_key']]['id'] if st.get('sub_line_key') else None)}, "
            f"{q(groups[line['project_key']]['id'])}, {q(bu_id)}, "
            f"{q(owner['name'] if owner else None)}, {q(owner['id'] if owner else None)}, {q(spoc['name'] if spoc else None)}, {q(spoc['id'] if spoc else None)});")
        add(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ({q(st['id'])}, {q(uid(st['team']))}) ON CONFLICT DO NOTHING;")

    add("\n-- Installed components (2 per station), serialized spares (550) and bulk stock")
    def item_insert(item_id, name, display, mfr, supplier, meta, serial, org, storage, status, is_stock, qty, min_qty, tech, machine_id, cost=None):
        add("INSERT INTO backend.inventory_items (id, name, display_name, manufacturer_id, supplier_id, metadata, serial_number, organization_id, "
            "storage_location, equipment_status, is_stock_item, stock_quantity, min_stock_threshold, technology, machine_id, cost_in_huf) "
            f"VALUES ({q(item_id)}, {q(name)}, {q(display)}, {q(uid(mfr))}, {q(uid(supplier))}, {qj(meta)}, {q(serial)}, {q(org)}, {q(storage)}, "
            f"{q(status)}, {q(is_stock)}, {q(qty)}, {q(min_qty)}, {q(tech)}, {q(machine_id)}, {q(cost)});")

    for c in model["components"]:
        st = station_by_name[c["station"]]
        item_insert(c["id"], c["name"], c["display_name"], c["manufacturer"], "Direct Automation Europe", c["metadata"], c["serial"],
                    c["org_id"], c["storage"], "InMachine", False, None, None, c["technology"], st["id"])
        if c["kind"] == "HardwareComponent":
            add(f"INSERT INTO backend.hardware_assets (id, model_number) VALUES ({q(c['id'])}, {q(PART_CLASSES[c['part_class']]['code'])});")
        else:
            sw_type = 2 if c["category"] == "SoftwareRuntime" else 3
            add(f"INSERT INTO backend.software_assets (id, version, license_key, software_type, status) VALUES ({q(c['id'])}, "
                f"{q(c['metadata'].get('Version'))}, {q(c['metadata'].get('LicenseKey'))}, {sw_type}, 'Active');")
        add(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ({q(c['id'])}, {q(uid(c['team']))}) ON CONFLICT DO NOTHING;")
    maint_team = uid("Plant Maintenance (Synthetic AI Guild)")
    for s in model["spares"]:
        item_insert(s["id"], s["name"], s["display_name"], s["manufacturer"], "Direct Automation Europe", s["metadata"], s["serial"],
                    "org-maintenance", s["storage"], s["status"], False, None, None, s["technology"], None, s["cost"])
        add(f"INSERT INTO backend.hardware_assets (id, model_number) VALUES ({q(s['id'])}, {q(PART_CLASSES[s['part_class']]['code'])});")
        add(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ({q(s['id'])}, {q(maint_team)}) ON CONFLICT DO NOTHING;")
    for b in model["bulk"]:
        item_insert(b["id"], b["name"], b["display_name"], b["manufacturer"], "RS Components", b["metadata"], b["serial"],
                    "org-maintenance", b["storage"], "InStorage", True, b["quantity"], b["minimum"], b["technology"], None)
        add(f"INSERT INTO backend.hardware_assets (id) VALUES ({q(b['id'])});")
        add(f"INSERT INTO backend.\"ItemResponsibilities\" (managed_items_id, responsible_teams_id) VALUES ({q(b['id'])}, {q(maint_team)}) ON CONFLICT DO NOTHING;")

    add("\n-- Controller <-> station links (1-1, 1-n, m-n, n-1); the 'Primary' controller reports the station's process data")
    for link in model["links"]:
        add("INSERT INTO backend.\"StationControllers\" (id, client_pc_id, machine_id, role, created_at) "
            f"VALUES ({q(link['id'])}, {q(pc_by_host[link['controller']]['id'])}, {q(station_by_name[link['station']]['id'])}, {q(link['role'])}, NOW());")

    add("\n-- Machine component slots (bound to the digital-twin mesh of the same name) with their installed part")
    for c in model["components"]:
        st = station_by_name[c["station"]]
        eplan = {"DeviceTag": f"={st['line']}+{st['op']}-{c['slot']}", "FunctionText": c["component_name"], "Cad3dNodePath": c["slot"]}
        add("INSERT INTO backend.machine_components (id, machine_id, name, functional_slot_identifier, category, current_part_id, tags, "
            "attributes_json, eplan_cad_ref_json, installed_at, updated_at) "
            f"VALUES ({q(c['component_id'])}, {q(st['id'])}, {q(c['component_name'])}, {q(c['slot'])}, {q(c['category'])}, {q(c['id'])}, "
            f"{qarr(c['tags'])}, {qjt({'partClass': c['part_class']})}, {qjt(eplan)}, NOW() - INTERVAL '400 days', NOW());")

    add("\n-- Machine group memberships (line + technology fleet)")
    for m in model["memberships"]:
        add("INSERT INTO backend.machine_group_memberships (id, group_id, machine_id, assigned_at, assigned_by) "
            f"VALUES ({q(m['id'])}, {q(groups[m['group']]['id'])}, {q(station_by_name[m['station']]['id'])}, NOW(), {q(BUSINESS_UNIT[3])});")

    add("\n-- Skills, maintenance crews and their members")
    for s in model["skills"]:
        add("INSERT INTO backend.skill_definitions (id, code, name, description, category, issuer, validity_months, created_at) "
            f"VALUES ({q(s['id'])}, {q(s['code'])}, {q(s['name'])}, {q(s['description'])}, {q(s['category'])}, {q(s['issuer'])}, {s['validity_months']}, NOW());")
    for ps in model["personnel_skills"]:
        months = skill_by_code[ps["skill"]]["validity_months"]
        add("INSERT INTO backend.personnel_skills (id, user_id, skill_id, certified_at, expires_at, certificate_number, verified_by) "
            f"VALUES ({q(ps['id'])}, {q(ps['user'])}, {q(skill_by_code[ps['skill']]['id'])}, NOW() - INTERVAL '120 days', "
            f"NOW() - INTERVAL '120 days' + INTERVAL '{months} months', {q(ps['certificate'])}, {q(users[BUSINESS_UNIT[3]]['name'])});")
    for g in model["personnel_groups"]:
        add("INSERT INTO backend.personnel_groups (id, name, description, discipline, shift_pattern, organization_id, created_at) "
            f"VALUES ({q(g['id'])}, {q(g['name'])}, {q(g['description'])}, {q(g['discipline'])}, {q(g['shift_pattern'])}, 'org-maintenance', NOW());")
        for m in g["members"]:
            add("INSERT INTO backend.personnel_group_members (id, personnel_group_id, user_id, user_name, joined_at) "
                f"VALUES ({q(uid('pgm:' + g['key'] + ':' + m))}, {q(g['id'])}, {q(m)}, {q(users[m]['name'])}, NOW());")

    add("\n-- Dispatch rules (delegation recipes) and their targets")
    for r in model["recipes"]:
        crew = pg_by_key[r["crew"]]
        esc = pg_by_key[r["escalation"]]
        skill_ids = [skill_by_code[r["skill"]]["id"]] if r["skill"] else []
        skill_codes = [r["skill"]] if r["skill"] else []
        mg_ids = [groups[k]["id"] for k in r["machine_groups"]]
        add("INSERT INTO backend.delegation_recipes (id, name, description, discipline, trigger_event_category, allowed_schedules, "
            "required_skill_ids_json, required_skill_codes_json, primary_personnel_group_id, primary_user_id, primary_user_name, "
            "on_call_fallback_user_id, on_call_fallback_user_name, escalation_group_id, escalation_timeout_minutes, is_active, created_at, "
            "updated_at, is_complete_machine_dedication, is_technology_primary, machine_group_ids_json, maintenance_group_ids_json, technology) "
            f"VALUES ({q(r['id'])}, {q(r['name'])}, {q(r['description'])}, {q(r['discipline'])}, 'Error', 7, {qjt(skill_ids)}, {qjt(skill_codes)}, "
            f"{q(crew['id'])}, {q(r['primary_user'])}, {q(users[r['primary_user']]['name'])}, {q(r['on_call'])}, {q(users[r['on_call']]['name'])}, "
            f"{q(esc['id'])}, {r['escalation_timeout_minutes']}, true, NOW(), NOW(), false, true, {qjt(mg_ids)}, {qjt([crew['id']])}, {q(r['technology'])});")
        add("INSERT INTO backend.delegation_maintenance_groups (id, delegation_recipe_id, maintenance_group_id, assigned_at) "
            f"VALUES ({q(uid('dmg:' + r['key'] + ':' + r['crew']))}, {q(r['id'])}, {q(crew['id'])}, NOW());")
        for k in r["machine_groups"]:
            add("INSERT INTO backend.delegation_machine_groups (id, delegation_recipe_id, machine_group_id, assigned_at) "
                f"VALUES ({q(uid('dmgr:' + r['key'] + ':' + k))}, {q(r['id'])}, {q(groups[k]['id'])}, NOW());")
    for b in model["bindings"]:
        add("INSERT INTO backend.machine_delegation_bindings (id, machine_id, delegation_recipe_id, discipline, is_active, bound_at) "
            f"VALUES ({q(b['id'])}, {q(station_by_name[b['station']]['id'])}, {q(recipe_by_key[b['recipe']]['id'])}, {q(b['discipline'])}, true, NOW());")
    for r in model["technician_rules"]:
        target = groups[r["target_group"]]["id"] if r["target_group"] else r["target"]
        add("INSERT INTO backend.technician_rules (id, name, technician_id, technician_name, technician_email, scope_type, target_id, "
            "category_filter, backup_technician_id, backup_technician_name, assigned_by_role, created_at) "
            f"VALUES ({q(r['id'])}, {q(r['name'])}, {q(r['technician'])}, {q(users[r['technician']]['name'])}, {q(users[r['technician']]['email'])}, "
            f"{q(r['scope_type'])}, {q(target)}, {q(r['category'])}, {q(r['backup'])}, {q(users[r['backup']]['name'])}, {q(r['assigned_by_role'])}, NOW());")
    for ab in model["absences"]:
        add("INSERT INTO backend.shift_absences (id, technician_id, technician_name, reason, start_date, end_date, marked_by, "
            "backup_technician_id, backup_technician_name, active) "
            f"VALUES ({q(ab['id'])}, {q(ab['technician'])}, {q(users[ab['technician']]['name'])}, {q(ab['reason'])}, "
            f"NOW() + INTERVAL '{ab['start_h']} hours', NOW() + INTERVAL '{ab['end_h']} hours', {q(users[ab['marked_by']]['name'])}, "
            f"{q(ab['backup'])}, {q(users[ab['backup']]['name'])}, true);")

    add("\n-- Directory security group -> role mappings and approved AD OUs")
    for idp, gid, dname, role, org in SECURITY_GROUP_MAPPINGS:
        add("INSERT INTO backend.security_group_mappings (id, identity_provider, group_identifier, display_name, mapped_role, organization_id, "
            f"is_enabled, created_at, updated_at) VALUES ({q(uid(idp + ':' + gid))}, {q(idp)}, {q(gid)}, {q(dname)}, {q(role)}, {q(org)}, true, NOW(), NOW()) "
            "ON CONFLICT (id) DO NOTHING;")
    for ou in AD_OU_GOVERNANCE:
        add("INSERT INTO backend.ad_ou_governances (id, ou_path, access_level, is_approved, approved_by, approved_at, notes, created_at, updated_at) "
            f"VALUES ({q(uid('ou:' + ou))}, {q(ou)}, 'read_write', true, {q(OU_APPROVER)}, NOW(), 'Approved production corridor', NOW(), NOW()) "
            "ON CONFLICT (id) DO NOTHING;")

    add("\nCOMMIT;")
    return "\n".join(sql) + "\n"


# ======================================================================================
# Generation
# ======================================================================================
def _write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def generate_all(model=None):
    model = model or build_model()
    _write(CSV_FILE, render_csv(model))
    _write(TOPOLOGY_FILE, render_topology(model))
    _write(FIXTURE_FILE, render_fixture(model))
    _write(SQL_FILE, render_sql(model))
    print(f"✓ Generated enterprise dataset: {len(model['stations'])} machines on {len(model['lines'])} lines, "
          f"{len(model['controllers'])} IPCs ({len(model['links'])} controller links), {len(model['components'])} installed components, "
          f"{len(model['spares'])} serialized spares, {len(model['bulk'])} bulk consumables, {len(model['users']) + 1} users, "
          f"{len(model['personnel_groups'])} maintenance crews, {len(model['recipes'])} dispatch rules")
    for p in (CSV_FILE, TOPOLOGY_FILE, FIXTURE_FILE, SQL_FILE):
        print(f"  - {os.path.relpath(p, REPO_ROOT)}")
    return model


# Kept for callers of the previous API.
def generate_csv(output_path=CSV_FILE):
    model = build_model()
    _write(output_path, render_csv(model))
    return csv_rows(model), [(l["controller"], l["station"], l["role"]) for l in model["links"]]


def generate_sql(csv_path=CSV_FILE, output_path=SQL_FILE, pc_station_links=None):
    _write(output_path, render_sql(build_model()))


def generate_topology(output_path=TOPOLOGY_FILE, stations=None, pcs=None, links=None):
    _write(output_path, render_topology(build_model()))


# ======================================================================================
# Validation
# ======================================================================================
def check_model(model):
    """Referential-integrity checks on the in-memory model. Returns (errors, warnings, facts)."""
    errors, warnings = [], []
    err = errors.append
    users = model["users"]
    stations = model["stations"]
    station_names = {s["name"] for s in stations}
    hostnames = {p["hostname"] for p in model["controllers"]}
    org_ids = set(model["org_ids"])
    roles, roles_from_source = read_heimdall_roles()
    if not roles_from_source:
        warnings.append("SecurityConstants.cs not found - roles checked against the built-in list")

    def unique(label, values):
        seen, dup = set(), set()
        for v in values:
            (dup if v in seen else seen).add(v)
        if dup:
            err(f"duplicate {label}: {sorted(dup)[:5]}")

    # ---- counts the rest of the system relies on
    if len(stations) != 100:
        err(f"expected 100 stations, got {len(stations)}")
    if len(model["controllers"]) != 56:
        err(f"expected 56 IPCs, got {len(model['controllers'])}")
    if len(model["components"]) != 200:
        err(f"expected 200 installed components, got {len(model['components'])}")
    in_storage = [s for s in model["spares"] if s["status"] == "InStorage"]
    if len(model["spares"]) != 550 or len(in_storage) != 500:
        err(f"expected 550 serialized spares (500 in storage), got {len(model['spares'])} ({len(in_storage)})")
    if len(model["bulk"]) < 20:
        err(f"expected >= 20 bulk consumables, got {len(model['bulk'])}")

    # ---- identity uniqueness
    all_items = [s["name"] for s in stations] + [c["name"] for c in model["components"]] + \
                [s["name"] for s in model["spares"]] + [b["name"] for b in model["bulk"]]
    unique("inventory item name", all_items)
    unique("inventory item id", [uid(n) for n in all_items])
    unique("serial number", [s["serial"] for s in stations] + [c["serial"] for c in model["components"]] +
           [s["serial"] for s in model["spares"]] + [p["serial"] for p in model["controllers"]])
    unique("station floor-plan handle", [s["handle"] for s in stations])
    unique("station custom identifier", [s["custom_identifier"] for s in stations])
    unique("controller hostname", [p["hostname"] for p in model["controllers"]])
    unique("controller MAC", [p["mac"] for p in model["controllers"]])
    unique("controller IP", [p["ip"] for p in model["controllers"]])
    unique("controller machine identifier", [p["machine_identifier"] for p in model["controllers"]])
    unique("user id", list(users) + [ADMIN_USER["id"]])
    unique("user email", [u["email"].lower() for u in users.values()] + [ADMIN_USER["email"]])
    unique("username", [u["username"].lower() for u in users.values()] + [ADMIN_USER["username"]])
    unique("organization id", model["org_ids"])
    unique("machine group id", [g["id"] for g in model["groups"]])

    # ---- stations <-> controllers
    for st in stations:
        if st["machine_type"] not in MACHINE_TYPES:
            err(f"{st['name']}: unknown machine type {st['machine_type']}")
        if st["org_id"] not in org_ids:
            err(f"{st['name']}: unknown organization {st['org_id']}")
        primaries = [c for c in st["controllers"] if c[1] == "Primary"]
        if not st["controllers"]:
            err(f"{st['name']}: no controller - the station would never report telemetry")
        elif len(primaries) != 1:
            err(f"{st['name']}: needs exactly one Primary (reporting) controller, has {len(primaries)}")
        if len(st["components"]) != 2:
            err(f"{st['name']}: expected 2 installed components, has {len(st['components'])}")
        for person, label in ((st["owner"], "owner"), (st["spoc"], "SPOC")):
            if person is None:
                err(f"{st['name']}: no {label} (line organization has no suitable member)")
            elif st["org_id"] not in person["orgs"]:
                err(f"{st['name']}: {label} {person['id']} is not a member of {st['org_id']}")
    for pc in model["controllers"]:
        if not pc["stations"]:
            err(f"{pc['hostname']}: controls no station")
        if pc["home_station"] not in station_names:
            err(f"{pc['hostname']}: home station {pc['home_station']} does not exist")
        if pc["org_id"] not in org_ids:
            err(f"{pc['hostname']}: unknown organization {pc['org_id']}")
    for link in model["links"]:
        if link["controller"] not in hostnames or link["station"] not in station_names:
            err(f"controller link {link['controller']} -> {link['station']} is dangling")
    unique("controller link", [(l["controller"], l["station"]) for l in model["links"]])

    # ---- components <-> part classes <-> spares
    used_classes = set()
    for c in model["components"]:
        if c["station"] not in station_names:
            err(f"{c['name']}: station {c['station']} does not exist")
        if c["part_class"] not in PART_CLASSES:
            err(f"{c['name']}: unknown part class {c['part_class']}")
        if c["category"] not in COMPONENT_CATEGORIES:
            err(f"{c['name']}: unknown component category {c['category']}")
        if c["slot"] not in TWIN_SLOTS:
            err(f"{c['name']}: slot {c['slot']} is not a digital-twin mesh name")
        used_classes.add(c["part_class"])
    unique("component slot per station", [(c["station"], c["slot"]) for c in model["components"]])
    spare_classes = {}
    for s in model["spares"]:
        if s["status"] not in EQUIPMENT_STATUSES:
            err(f"{s['name']}: unknown equipment status {s['status']}")
        if s["status"] == "InStorage":
            spare_classes[s["part_class"]] = spare_classes.get(s["part_class"], 0) + 1
    for cls in sorted(used_classes):
        if PART_CLASSES[cls]["spare"] and not spare_classes.get(cls):
            err(f"part class {cls} is installed in stations but has no spare in storage")
    for cls in sorted(spare_classes):
        if cls not in used_classes:
            err(f"spares of class {cls} fit no installed component")
    for b in model["bulk"]:
        if b["quantity"] < 0 or b["minimum"] < 0:
            err(f"{b['name']}: negative stock")

    # ---- users, roles, memberships
    for u in list(users.values()) + [dict(ADMIN_USER, orgs=["org-platform", "org-controls"])]:
        if u["role"] not in roles:
            err(f"user {u['id']}: role {u['role']} is not a HeimdallRoles constant")
        if not u["orgs"]:
            err(f"user {u['id']}: no organization membership")
        for o in u["orgs"]:
            if o not in org_ids:
                err(f"user {u['id']}: membership in unknown organization {o}")
        if not u["email"].lower().endswith(tuple("@" + d for d in FAKE_EMAIL_DOMAINS)):
            err(f"user {u['id']}: e-mail {u['email']} is not on a reserved synthetic domain")
    if ADMIN_USER != {"id": "usr-admin-primary", "name": "System Administrator", "email": "admin@heimdall.dev",
                      "role": "system_admin", "username": "admin"}:
        err("dev admin account contract changed (usr-admin-primary / admin@heimdall.dev / system_admin)")
    for o in org_ids:
        if not any(o in u["orgs"] for u in users.values()):
            err(f"organization {o} has no member")

    # ---- machine groups
    group_keys = {g["key"] for g in model["groups"]}
    for g in model["groups"]:
        if g["parent"] and g["parent"] not in group_keys:
            err(f"machine group {g['key']}: unknown parent {g['parent']}")
        if g["lead"] is None and g["type"] != "Technological":
            err(f"machine group {g['key']}: no lead engineer")
        for name in g["machines"]:
            if name not in station_names:
                err(f"machine group {g['key']}: unknown station {name}")
        if g["type"] in ("Line", "Technological") and not g["machines"]:
            err(f"machine group {g['key']}: empty")
    parent_type = {"Project": "BusinessUnit", "Line": "Project", "SubLine": "Line"}
    by_key = {g["key"]: g for g in model["groups"]}
    for g in model["groups"]:
        want = parent_type.get(g["type"])
        if want and (g["parent"] not in by_key or by_key[g["parent"]]["type"] != want):
            err(f"machine group {g['key']}: a {g['type']} group must hang under a {want} group")
    for st in stations:
        sub = st.get("sub_line_key")
        if sub and by_key[sub]["parent"] != f"grp-line-{st['line_num']:02d}":
            err(f"{st['name']}: sub-line {sub} belongs to another line")
    in_line = {n for g in model["groups"] if g["type"] == "Line" for n in g["machines"]}
    if in_line != station_names:
        err(f"{len(station_names - in_line)} station(s) are in no line group")
    in_fleet = {n for g in model["groups"] if g["type"] == "Technological" for n in g["machines"]}
    if in_fleet != station_names:
        err(f"{len(station_names - in_fleet)} station(s) are in no technology fleet")

    # ---- crews, skills, dispatch
    skill_codes = {s["code"] for s in model["skills"]}
    pg_keys = {g["key"] for g in model["personnel_groups"]}
    for s in model["skills"]:
        if s["category"] not in SKILL_CATEGORIES:
            err(f"skill {s['code']}: unknown category {s['category']}")
    for g in model["personnel_groups"]:
        if g["discipline"] not in DISCIPLINES:
            err(f"crew {g['key']}: unknown discipline {g['discipline']}")
        if g["skill"] and g["skill"] not in skill_codes:
            err(f"crew {g['key']}: unknown skill {g['skill']}")
        if not g["members"]:
            err(f"crew {g['key']}: no members")
        for m in g["members"]:
            if m not in users:
                err(f"crew {g['key']}: unknown member {m}")
    technologies = {s["technology"] for s in stations}
    for r in model["recipes"]:
        if r["discipline"] not in DISCIPLINES:
            err(f"recipe {r['key']}: unknown discipline {r['discipline']}")
        if r["crew"] not in pg_keys or r["escalation"] not in pg_keys:
            err(f"recipe {r['key']}: unknown crew / escalation group")
        for person in (r["primary_user"], r["on_call"]):
            if person not in users:
                err(f"recipe {r['key']}: unknown user {person}")
        if r["skill"] and r["skill"] not in skill_codes:
            err(f"recipe {r['key']}: unknown skill {r['skill']}")
        for k in r["machine_groups"]:
            if k not in group_keys:
                err(f"recipe {r['key']}: unknown machine group {k}")
    # Recipes that are matched by technology must not carry a discipline the backend infers from alarms
    # (Mechanical / Controls / Electrical): such a recipe would also match unrelated machines.
    for r in model["recipes"]:
        if not r["machine_groups"] and r["discipline"] in ("Mechanical", "Controls", "Electrical"):
            err(f"recipe {r['key']}: global technology recipe must not use discipline {r['discipline']}")
    for tech in sorted(technologies):
        if not any(r["technology"] == tech for r in model["recipes"]):
            err(f"station technology '{tech}' has no dispatch rule - machine-raised tickets would be unassigned")
    for b in model["bindings"]:
        if b["station"] not in station_names or b["recipe"] not in {r["key"] for r in model["recipes"]}:
            err(f"delegation binding {b['id']} is dangling")
    for r in model["technician_rules"]:
        for person in (r["technician"], r["backup"]):
            if person not in users:
                err(f"technician rule {r['key']}: unknown user {person}")
        if r["target_group"] and r["target_group"] not in group_keys:
            err(f"technician rule {r['key']}: unknown machine group {r['target_group']}")
    for a in model["absences"]:
        for person in (a["technician"], a["marked_by"], a["backup"]):
            if person not in users:
                err(f"absence {a['key']}: unknown user {person}")
        if a["end_h"] <= a["start_h"]:
            err(f"absence {a['key']}: ends before it starts")

    # ---- directory mappings
    for idp, gid, dname, role, org in SECURITY_GROUP_MAPPINGS:
        if role not in roles:
            err(f"security group mapping {dname}: role {role} is not a HeimdallRoles constant")
        if org not in org_ids:
            err(f"security group mapping {dname}: unknown organization {org}")
    org_names = {o["name"] for o in ORGANIZATIONS}
    fixture_group_idents = set()
    for gid, ident, disp, idp, role, org_name, org_role, members in FIXTURE_SECURITY_GROUPS:
        fixture_group_idents.add(ident)
        if role not in roles:
            err(f"fixture security group {gid}: role {role} is not a HeimdallRoles constant")
        if org_name not in org_names:
            err(f"fixture security group {gid}: unknown organization name {org_name!r}")
        for m in members:
            if m not in users:
                err(f"fixture security group {gid}: unknown member {m}")
    for org, ident in ORG_TO_SG.items():
        if org not in org_ids or ident not in fixture_group_idents:
            err(f"organization -> security group mapping for {org} is dangling")
    for ou in DISCIPLINE_OUS:
        for h in ou["candidateHostnames"]:
            if h not in hostnames:
                err(f"AD OU {ou['name']}: unknown host {h}")

    # ---- floor plan and digital twin
    facts = {}
    if os.path.exists(FLOOR_PLAN_DXF):
        dxf_handles = set(read_dxf_insert_handles(FLOOR_PLAN_DXF))
        facts["dxf_handles"] = len(dxf_handles)
        for st in stations:
            if st["handle"] not in dxf_handles:
                err(f"{st['name']}: floor-plan handle {st['handle']} is not in {os.path.basename(FLOOR_PLAN_DXF)}")
        st_by_name = {s["name"]: s for s in stations}
        for pc in model["controllers"]:
            if st_by_name[pc["home_station"]]["handle"] not in dxf_handles:
                err(f"{pc['hostname']}: pinned handle is not in the floor plan")
    else:
        warnings.append(f"floor plan {os.path.relpath(FLOOR_PLAN_DXF, REPO_ROOT)} not found - CAD handles not checked")
    if os.path.exists(DIGITAL_TWIN_GLB):
        nodes = set(read_glb_node_names(DIGITAL_TWIN_GLB))
        facts["twin_nodes"] = len(nodes)
        if set(TWIN_SLOTS) - nodes:
            err(f"digital twin model lacks mesh node(s) {sorted(set(TWIN_SLOTS) - nodes)}")
    else:
        warnings.append("digital twin model not found - component slots not checked against mesh names")
    return errors, warnings, facts


def check_generated_files(model):
    """The committed artifacts must equal a fresh render (they are generated, never hand-edited)."""
    errors = []
    for path, render, required in ((CSV_FILE, render_csv, True), (TOPOLOGY_FILE, render_topology, True),
                                   (FIXTURE_FILE, render_fixture, True), (SQL_FILE, render_sql, False)):
        rel = os.path.relpath(path, REPO_ROOT)
        if not os.path.exists(path):
            if required:
                errors.append(f"{rel} is missing (run seed_pipeline.py --generate-all)")
            continue  # incremental_seed.sql is git-ignored; it is regenerated by `just db-seed`
        with open(path, encoding="utf-8", newline="") as f:
            if f.read() != render(model):
                errors.append(f"{rel} is stale or hand-edited (run seed_pipeline.py --generate-all)")
    return errors


def validate():
    print("=== Validating Seed Data Referential Integrity (enterprise dataset) ===")
    model = build_model()
    errors, warnings, facts = check_model(model)
    errors += check_generated_files(model)

    sql = render_sql(model)
    for table in ("maintenance_tickets", "ticket_comments", "machine_service_logs", "component_part_histories", "agent_events"):
        if f"INSERT INTO backend.{table}" in sql:
            errors.append(f"seed SQL inserts into {table}: activity must come from the simulator, not from the seed")
    if build_model() != model or render_sql(build_model()) != sql:
        errors.append("generator is not deterministic")

    for w in warnings:
        print(f"  WARNING: {w}")
    if errors:
        for e in errors:
            print(f"  FAIL: {e}")
        print(f"✗ {len(errors)} integrity error(s)")
        return False

    roles, _ = read_heimdall_roles()
    technologies = sorted({s["technology"] for s in model["stations"]})
    print(f"✓ Inventory: {len(model['stations'])} machines, {len(model['controllers'])} IPCs, {len(model['components'])} installed components, "
          f"{len(model['spares'])} serialized spares (500 in storage, 50 under repair), {len(model['bulk'])} bulk consumables")
    print(f"✓ Every station has exactly one reporting controller ({len(model['links'])} links, 4 control topologies); "
          f"every IPC controls at least one station")
    print(f"✓ Components: {len(model['components'])} slots bound to an installed part and a digital-twin mesh; "
          f"{len(SPARE_CLASS_ORDER)} spare classes all fit installed components")
    if "dxf_handles" in facts:
        print(f"✓ Floor plan: all {len(model['stations'])} station handles and {len(model['controllers'])} controller pins resolve "
              f"in production_hall.dxf ({facts['dxf_handles']} block handles)")
    print(f"✓ Identity: {len(model['users']) + 1} users with roles from HeimdallRoles ({len(roles)} constants), "
          f"{len(model['org_ids'])} organizations, every user and organization has a membership")
    print(f"✓ Dispatch: {len(model['personnel_groups'])} crews, {len(model['skills'])} skills, {len(model['recipes'])} recipes covering "
          f"technologies {', '.join(technologies)}")
    print(f"✓ Machine hierarchy: {len(model['groups'])} groups (business unit > projects > lines > sub-lines, technology fleets), "
          f"{len(model['memberships'])} memberships")
    print("✓ Generated files are up to date and the seed contains no activity rows (tickets, logs, events)")
    print("✓ All Seed Data integrity checks PASSED successfully!")
    return True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Heimdall Unified Seed Data Pipeline (enterprise dataset)")
    parser.add_argument("--generate-all", action="store_true", help="Regenerate CSV, topology, fixture and SQL seed files")
    parser.add_argument("--validate", action="store_true", help="Validate referential integrity and that the committed files are current")
    args = parser.parse_args()

    # `--generate-all` regenerates, `--validate` only checks the committed files; no flag does both.
    if args.generate_all or not args.validate:
        generate_all()
    if not validate():
        sys.exit(1)
