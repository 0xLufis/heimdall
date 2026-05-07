import csv
import random
import json

def generate_massive_csv(output_path):
    manufacturers = ["RoboCorp", "Cyberdyne", "Omni Consumer Products", "Dell", "HP", "Intel", "Samsung", "KUKA", "FANUC", "Keyence", "Beckhoff", "Siemens", "Sick", "Cognex"]
    suppliers = ["Industrial Automata Direct", "FutureTech Solutions", "Global Components Ltd", "Automation World", "TechSupply Co"]
    teams = ["Mechanical", "Controls", "Vision", "Dispensing", "IT", "Maintenance", "Safety"]
    
    headers = ["Type", "Name", "DisplayName", "ResponsibleTeam", "Manufacturer", "Supplier", "SerialNumber", "ParentName", "StationIdentifier", "ClientPcHostname", "Metadata"]
    
    rows = []
    
    # 1. Generate 50 Lines (Machines)
    for i in range(1, 51):
        line_name = f"Production Line {i:02d}"
        display_name = f"Main Assembly {i:02d}"
        selected_teams = random.sample(teams, k=random.randint(1, 3))
        mfr = random.choice(manufacturers[:3]) # Use heavier industrial mfrs for lines
        sup = random.choice(suppliers)
        sn = f"LINE-SN-{i:04d}"
        metadata = json.dumps({"Zone": random.choice(["North", "South", "East", "West"]), "Capacity": f"{random.randint(100, 500)} units/hr"})
        
        rows.append(["Machine", line_name, display_name, ";".join(selected_teams), mfr, sup, sn, "", line_name, "", metadata])
        
        # 2. For each Line, generate 1-2 ClientPcs
        pc_count = random.randint(1, 2)
        pc_hostnames = []
        for p in range(1, pc_count + 1):
            pc_hostname = f"L{i:02d}-PC-{p:02d}"
            pc_display = f"Controller {p} for Line {i:02d}"
            pc_mfr = random.choice(["Dell", "HP", "Beckhoff", "Siemens"])
            pc_sn = f"PC-SN-{i:02d}-{p:02d}-{random.randint(1000, 9999)}"
            pc_metadata = json.dumps({"IP": f"10.1.{i}.{10+p}", "OS": "Windows 10 IoT"})
            
            rows.append(["ClientPc", pc_hostname, pc_display, "IT", pc_mfr, "FutureTech Solutions", pc_sn, "", line_name, "", pc_metadata])
            pc_hostnames.append(pc_hostname)
            
            # 3. For each PC, generate internal hardware
            pc_parts = [
                ("CPU", ["Intel i7", "Intel i9", "AMD Ryzen 7"], "IT"),
                ("RAM", ["16GB DDR4", "32GB DDR4", "64GB DDR4"], "IT"),
                ("SSD", ["512GB NVMe", "1TB NVMe"], "IT"),
                ("NIC", ["Dual 1GbE", "Single 10GbE"], "IT")
            ]
            for part_name, variants, team in pc_parts:
                part_id = f"{pc_hostname}-{part_name}"
                part_variant = random.choice(variants)
                rows.append(["PcHardware", part_id, part_variant, team, random.choice(["Intel", "Samsung", "Dell"]), sup, "", pc_hostname, "", pc_hostname, "{}"])

        # 4. Generate Hardware Components for the Line
        hw_types = [
            ("Vision Sensor", ["4K Camera", "2D Scanner", "Depth Sensor"], "Vision"),
            ("Servo Motor", ["X-Axis Drive", "Y-Axis Drive", "Z-Axis Rotary"], "Mechanical"),
            ("Valve Island", ["Pneumatic 8-way", "Digital Manifold"], "Mechanical"),
            ("Safety Laser", ["Scanner Zone A", "Scanner Zone B"], "Safety"),
            ("PLC Node", ["IO-Link Master", "Distributed IO"], "Controls")
        ]
        
        for h in range(1, random.randint(6, 12)):
            hw_base, hw_variants, hw_team = random.choice(hw_types)
            hw_name = f"L{i:02d}-{hw_base}-{h:02d}"
            hw_display = random.choice(hw_variants)
            hw_mfr = random.choice(manufacturers)
            hw_sn = f"HW-SN-{i:02d}-{h:02d}-{random.randint(1000, 9999)}"
            
            # Link to the Line and a PC if it's an IO device
            target_pc = random.choice(pc_hostnames)
            rows.append(["HardwareComponent", hw_name, hw_display, hw_team, hw_mfr, sup, hw_sn, "", line_name, target_pc, "{}"])
            
            # 5. Add some software/firmware to these hardware items
            if random.random() > 0.5:
                fw_name = f"{hw_name}-Firmware"
                rows.append(["SoftwareComponent", fw_name, "V" + str(random.randint(1, 5)) + ".0", hw_team, hw_mfr, "", "", hw_name, line_name, "", "{}"])

        # 6. Add some top-level software for the Line
        sw_name = f"L{i:02d}-Logic-Program"
        rows.append(["SoftwareComponent", sw_name, "PLC Project Main", "Controls", "Siemens", "", "", "", line_name, "", json.dumps({"Version": "1.0.5"})])

    with open(output_path, mode='w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(headers)
        writer.writerows(rows)
    
    print(f"Generated {len(rows)} items across 50 lines in {output_path}")

if __name__ == "__main__":
    generate_massive_csv('seed_data/inventory_seed.csv')
