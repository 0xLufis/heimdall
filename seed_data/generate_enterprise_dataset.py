import csv
import json
import random
import uuid

# Set seed for reproducible, professional data generation
random.seed(42)

def generate():
    print("Generating enterprise-scale dataset: 500 Client PCs, 500 Stations, 15 Production Lines, 90 Users...")

    manufacturers = ["Siemens", "Beckhoff", "Fanuc", "Cognex", "Keyence", "Festo", "Omron", "Dell", "HP", "Cisco", "Phoenix Contact", "Advantech", "Intel", "AMD", "Kingston", "Samsung"]
    suppliers = ["Insight", "Industrial Automata Direct", "Farnell", "RS Components", "MISUMI", "Conrad Electronic"]
    teams = [
        "Controls Engineering",
        "Vision Systems",
        "Robotics Dept",
        "Maintenance Team",
        "IT Infrastructure",
        "Quality Assurance",
        "Logistics"
    ]
    orgs = ["Production Floor A", "Production Floor B", "Production Floor C", "Production Floor D"]

    lines = [
        "Line 01 - Body Assembly Alpha",
        "Line 02 - Robotic Welding Cell",
        "Line 03 - Precision Machining & Milling",
        "Line 04 - High-Speed Stamping & Press",
        "Line 05 - Powertrain Sub-Assembly",
        "Line 06 - Automated Battery Module Line",
        "Line 07 - Electronics & SMT Placement",
        "Line 08 - Surface Coating & Paint Shop",
        "Line 09 - Optical Quality Inspection",
        "Line 10 - End-of-Line EOL Testing",
        "Line 11 - Automated Packaging & Boxing",
        "Line 12 - High-Bay Warehousing & AGVs",
        "Line 13 - Chemical Treatment & Plating",
        "Line 14 - Final Vehicle Integration",
        "Line 15 - Logistics & Palletizing Cell"
    ]

    dxf_handles = ["H-AL1", "H-WS5", "L-SORT-A", "P-LINE-B", "Q-CELL-01", "C-TANK-4", "CNC-MC-12", "P-LINE-C", "T-CELL-01", "P-CELL-02"]

    rows = []

    # --- 1. Generate 500 Production Stations / Machines ---
    stations = []
    for i in range(1, 501):
        line_idx = (i - 1) % len(lines)
        line_name = lines[line_idx]
        op_num = ((i - 1) % 33 + 1) * 10
        st_name = f"L{(line_idx+1):02d}-OP{op_num}"
        st_display = f"{line_name} - Station {op_num}"
        st_team = random.choice(teams) + ";" + random.choice(teams)
        mfr = random.choice(["Siemens", "Beckhoff", "Fanuc", "Festo", "Omron", "Cognex"])
        sup = random.choice(suppliers)
        sn = f"SN-STA-{i:04d}"
        handle = dxf_handles[(i - 1) % len(dxf_handles)]
        org = orgs[(line_idx) % len(orgs)]
        
        meta = {
            "Line": line_name,
            "CycleTimeTarget": f"{random.randint(20, 90)}s",
            "SafetyRating": random.choice(["SIL2", "SIL3", "PLd", "PLe"]),
            "PowerSupply": random.choice(["400V 3Ph 50Hz", "230V 1Ph 50Hz", "24V DC Industrial"])
        }
        
        stations.append({
            "Type": "Machine",
            "Name": st_name,
            "DisplayName": st_display,
            "ResponsibleTeam": st_team,
            "Manufacturer": mfr,
            "Supplier": sup,
            "SerialNumber": sn,
            "ParentName": "",
            "StationIdentifier": st_name,
            "ClientPcHostname": "",
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": handle,
            "OrganizationId": org
        })

    rows.extend(stations)

    # --- 2. Generate 500 Client PCs ---
    pcs = []
    pc_models = [
        ("SIMATIC IPC477E", "Siemens", "Windows 10 IoT Enterprise 2021 LTSC"),
        ("Embedded PC C6030", "Beckhoff", "Windows 11 IoT Enterprise LTSC 2024"),
        ("OptiPlex 7090 Micro", "Dell", "Ubuntu 24.04 LTS"),
        ("Z2 Mini G9 Workstation", "HP", "Windows 11 Pro Workstation"),
        ("UNO-2484G Fanless Edge", "Advantech", "Debian 12 Bookworm")
    ]

    software_suites = [
        "Beckhoff TwinCAT 3.1 Build 4026.10;Siemens TIA Portal V19;Wireshark 4.2.4;TcRTEthernet Driver v3.1",
        "Cognex In-Sight Explorer 6.5.0;Fanuc ROBOGUIDE V9.40;Kepware KEPServerEX 6.14",
        "Festo Automation Suite 2.6.0;Omron Sysmac Studio 1.54;Visual Studio 2022 Community",
        "Siemens WinCC Advanced V19;Node-RED v3.1;Beckhoff TwinCAT 3.1;Mosquitto MQTT Broker"
    ]

    for i in range(1, 501):
        hostname = f"CPC-{i:03d}"
        display_name = f"Edge Terminal {hostname}"
        assoc_station = stations[i - 1]["Name"]
        assoc_org = stations[i - 1]["OrganizationId"]
        model_name, mfr, os_ver = pc_models[(i - 1) % len(pc_models)]
        sw = software_suites[(i - 1) % len(software_suites)]
        handle = stations[i - 1]["PinnedObjectHandle"]

        meta = {
            "IPAddress": f"10.0.{(i // 254) + 1}.{(i % 254) + 1}",
            "OperatingSystem": os_ver,
            "Model": model_name,
            "InstalledSoftware": sw,
            "BeckhoffRtDriver": "TcRTEthernet active" if "TwinCAT" in sw else "Standard NIC",
            "SecurityPolicy": random.choice(["Enforced TISAX AL3", "Enforced TISAX AL2", "Standard Operational Domain"])
        }

        pcs.append({
            "Type": "ClientPc",
            "Name": hostname,
            "DisplayName": display_name,
            "ResponsibleTeam": "IT Infrastructure;Controls Engineering",
            "Manufacturer": mfr,
            "Supplier": "Insight",
            "SerialNumber": f"SN-PC-{i:04d}",
            "ParentName": "",
            "StationIdentifier": assoc_station,
            "ClientPcHostname": hostname,
            "Metadata": json.dumps(meta),
            "PinnedObjectHandle": handle,
            "OrganizationId": assoc_org
        })
        
        # Link station to this PC
        stations[i - 1]["ClientPcHostname"] = hostname

    rows.extend(pcs)

    # --- 3. Generate Hardware & Software Sub-Components ---
    components = []
    comp_templates = [
        ("PLC-S7-1500", "Siemens S7-1500 PLC", "HardwareComponent", "Siemens", json.dumps({"Model": "1516-3 PN/DP", "Memory": "1MB Code", "Rack": 1})),
        ("CAM-Cognex-9000", "Cognex In-Sight 9000 Vision Camera", "HardwareComponent", "Cognex", json.dumps({"Resolution": "12MP", "Lens": "16mm", "Illumination": "Red LED"})),
        ("DRV-AX5000", "Beckhoff AX5000 Servo Drive", "HardwareComponent", "Beckhoff", json.dumps({"Current": "12A", "Feedback": "EnDat 2.2", "Channels": 2})),
        ("ROB-Fanuc-M20", "Fanuc M-20iB Robot Controller", "HardwareComponent", "Fanuc", json.dumps({"Payload": "20kg", "Reach": "1811mm", "DOF": 6})),
        ("VAL-Festo-VTUG", "Festo VTUG Valve Terminal", "HardwareComponent", "Festo", json.dumps({"Valves": 12, "Bus": "Profinet", "Pressure": "6.0Bar"})),
        ("SWI-Cisco-IE3300", "Cisco Catalyst IE3300 Industrial Switch", "HardwareComponent", "Cisco", json.dumps({"Ports": 10, "PoE": True, "Speed": "1Gbps"})),
        ("SW-TwinCAT3", "Beckhoff TwinCAT 3 Runtime License", "SoftwareComponent", "Beckhoff", json.dumps({"LicenseKey": "TC3-RT-ENTERPRISE-500", "Version": "3.1.4026"})),
        ("SW-TIAPortal", "Siemens TIA Portal V19 License", "SoftwareComponent", "Siemens", json.dumps({"LicenseKey": "TIA-V19-FLOATING-500", "Version": "19.0.0"}))
    ]

    for i in range(1, 501):
        st_name = stations[i - 1]["Name"]
        pc_name = pcs[i - 1]["Name"]
        org = stations[i - 1]["OrganizationId"]

        # Add 2 hardware/software sub-components per station
        for j in range(2):
            tmpl_name, tmpl_disp, tmpl_type, tmpl_mfr, tmpl_meta = comp_templates[(i + j) % len(comp_templates)]
            comp_name = f"{tmpl_name}-S{i:03d}-{j+1}"
            comp_disp = f"{tmpl_disp} ({st_name})"
            
            components.append({
                "Type": tmpl_type,
                "Name": comp_name,
                "DisplayName": comp_disp,
                "ResponsibleTeam": stations[i - 1]["ResponsibleTeam"],
                "Manufacturer": tmpl_mfr,
                "Supplier": "Industrial Automata Direct",
                "SerialNumber": f"SN-CMP-{i:04d}-{j+1}",
                "ParentName": st_name,
                "StationIdentifier": st_name,
                "ClientPcHostname": pc_name,
                "Metadata": tmpl_meta,
                "PinnedObjectHandle": "",
                "OrganizationId": org
            })

    rows.extend(components)

    # Write to seed_data/inventory_seed.csv
    csv_file = "seed_data/inventory_seed.csv"
    fieldnames = ["Type", "Name", "DisplayName", "ResponsibleTeam", "Manufacturer", "Supplier", "SerialNumber", "ParentName", "StationIdentifier", "ClientPcHostname", "Metadata", "PinnedObjectHandle", "OrganizationId"]
    
    with open(csv_file, mode="w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"Successfully generated {len(rows)} enterprise inventory entries in {csv_file}!")

if __name__ == "__main__":
    generate()
