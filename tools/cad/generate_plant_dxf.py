#!/usr/bin/env python3
"""
Heimdall Industrial CAD / DXF Plant Floorplan Generator
Generates realistic multi-line automated assembly layout with conveyor loops,
pallet carriers, robotic safety fences, and 100 interactive station handles.
"""

import os
import ezdxf
from ezdxf import colors
from ezdxf.enums import TextEntityAlignment

def build_dxf():
    doc = ezdxf.new('R2010')
    msp = doc.modelspace()

    # Define CAD Layers
    doc.layers.add("WALLS", color=colors.CYAN)
    doc.layers.add("CONVEYORS", color=colors.YELLOW)
    doc.layers.add("PALLETS", color=colors.WHITE)
    doc.layers.add("ROBOT_CELLS", color=colors.MAGENTA)
    doc.layers.add("STATIONS", color=colors.GREEN)
    doc.layers.add("SAFETY_FENCING", color=colors.RED)
    doc.layers.add("STORAGE", color=colors.BLUE)
    doc.layers.add("TEXT", color=colors.WHITE)

    # 1. Main Plant Outer Perimeter (2400 x 1700 units)
    msp.add_lwpolyline([(0, 0), (2400, 0), (2400, 1700), (0, 1700), (0, 0)], dxfattribs={'layer': 'WALLS'})

    # Dividing wall between production halls (x = 1350)
    msp.add_lwpolyline([(1350, 0), (1350, 1700)], dxfattribs={'layer': 'WALLS'})

    # Floor / Department Titles
    msp.add_text("HEIMDALL AUTOMATED SMART FACTORY – GIGA-01", height=24).set_placement((1200, 1660), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
    msp.add_text("AUTOMATED PRODUCTION CORRIDOR (LINES 01 - 08)", height=16).set_placement((650, 1620), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
    msp.add_text("HIGH-BAY LOGISTICS & SPARE PARTS WAREHOUSE (550 SERIALIZED SLOTS)", height=16).set_placement((1875, 1620), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # --- 2. Define Reusable CAD Blocks ---

    # A. Pallet Conveyor Station Bed
    conveyor_blk = doc.blocks.new(name='CONVEYOR_BED')
    conveyor_blk.add_lwpolyline([(-14, -8), (14, -8), (14, 8), (-14, 8), (-14, -8)], dxfattribs={'layer': 'CONVEYORS'})
    conveyor_blk.add_line((-14, -4), (14, -4), dxfattribs={'layer': 'CONVEYORS'})
    conveyor_blk.add_line((-14, 4), (14, 4), dxfattribs={'layer': 'CONVEYORS'})
    # Pallet on top
    conveyor_blk.add_lwpolyline([(-8, -6), (8, -6), (8, 6), (-8, 6), (-8, -6)], dxfattribs={'layer': 'PALLETS'})
    conveyor_blk.add_circle((0, 0), 2, dxfattribs={'layer': 'PALLETS'})
    conveyor_blk.add_text("PALLET", height=1.5).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # B. Robotic Assembly Cell
    robot_blk = doc.blocks.new(name='ROBOT_CELL')
    robot_blk.add_lwpolyline([(-16, -16), (16, -16), (16, 16), (-16, 16), (-16, -16)], dxfattribs={'layer': 'SAFETY_FENCING', 'linetype': 'DASHED'})
    robot_blk.add_circle((0, 0), 5, dxfattribs={'layer': 'ROBOT_CELLS'})
    robot_blk.add_line((0, 0), (9, 9), dxfattribs={'layer': 'ROBOT_CELLS'})
    robot_blk.add_circle((9, 9), 2, dxfattribs={'layer': 'ROBOT_CELLS'})
    robot_blk.add_text("6-AXIS ROBOT", height=2.0).set_placement((0, -12), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # C. Dispensing Cell
    disp_blk = doc.blocks.new(name='DISPENSING_CELL')
    disp_blk.add_lwpolyline([(-14, -12), (14, -12), (14, 12), (-14, 12), (-14, -12)], dxfattribs={'layer': 'STATIONS'})
    disp_blk.add_circle((0, 0), 4, dxfattribs={'layer': 'STATIONS'})
    disp_blk.add_line((-10, 0), (10, 0), dxfattribs={'layer': 'STATIONS'})
    disp_blk.add_line((0, -8), (0, 8), dxfattribs={'layer': 'STATIONS'})
    disp_blk.add_text("2K DISPENSER", height=1.8).set_placement((0, -10), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # D. Fastening & Screwing Station
    fast_blk = doc.blocks.new(name='FASTENING_STATION')
    fast_blk.add_lwpolyline([(-12, -10), (12, -10), (12, 10), (-12, 10), (-12, -10)], dxfattribs={'layer': 'STATIONS'})
    for cx in [-5, 5]:
        for cy in [-4, 4]:
            fast_blk.add_circle((cx, cy), 1.5, dxfattribs={'layer': 'STATIONS'})
    fast_blk.add_text("TORQUE FASTEN", height=1.8).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # E. Laser Welding Cell
    laser_blk = doc.blocks.new(name='LASER_WELD_CELL')
    laser_blk.add_lwpolyline([(-16, -14), (16, -14), (16, 14), (-16, 14), (-16, -14)], dxfattribs={'layer': 'SAFETY_FENCING'})
    laser_blk.add_line((-10, -8), (10, 8), dxfattribs={'layer': 'STATIONS'})
    laser_blk.add_line((-10, 8), (10, -8), dxfattribs={'layer': 'STATIONS'})
    laser_blk.add_circle((0, 0), 3, dxfattribs={'layer': 'STATIONS'})
    laser_blk.add_text("LASER WELD", height=1.8).set_placement((0, -11), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # F. Servo Press Station
    press_blk = doc.blocks.new(name='SERVO_PRESS')
    press_blk.add_lwpolyline([(-12, -12), (12, -12), (12, 12), (-12, 12), (-12, -12)], dxfattribs={'layer': 'STATIONS'})
    press_blk.add_circle((0, 0), 6, dxfattribs={'layer': 'STATIONS'})
    press_blk.add_text("SERVO PRESS", height=1.8).set_placement((0, -9), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # G. Vision QA Inspection Booth
    vision_blk = doc.blocks.new(name='VISION_INSPECTION')
    vision_blk.add_lwpolyline([(-14, -10), (14, -10), (14, 10), (-14, 10), (-14, -10)], dxfattribs={'layer': 'STATIONS'})
    vision_blk.add_circle((0, 0), 3, dxfattribs={'layer': 'STATIONS'})
    vision_blk.add_circle((0, 0), 6, dxfattribs={'layer': 'STATIONS', 'linetype': 'DASHED'})
    vision_blk.add_text("3D VISION AOI", height=1.8).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # H. EOL Testing Station
    eol_blk = doc.blocks.new(name='EOL_TEST_BENCH')
    eol_blk.add_lwpolyline([(-15, -12), (15, -12), (15, 12), (-15, 12), (-15, -12)], dxfattribs={'layer': 'STATIONS'})
    eol_blk.add_lwpolyline([(-8, -6), (8, -6), (8, 6), (-8, 6), (-8, -6)], dxfattribs={'layer': 'STATIONS'})
    eol_blk.add_text("EOL TEST BENCH", height=1.8).set_placement((0, -9), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # I. Warehouse Storage Rack Block
    rack_blk = doc.blocks.new(name='WAREHOUSE_RACK')
    rack_blk.add_lwpolyline([(-30, -12), (30, -12), (30, 12), (-30, 12), (-30, -12)], dxfattribs={'layer': 'STORAGE'})
    for rx in [-20, -10, 0, 10, 20]:
        rack_blk.add_line((rx, -12), (rx, 12), dxfattribs={'layer': 'STORAGE'})
    rack_blk.add_text("STORAGE RACK", height=2.0).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

    # --- 3. Render 8 Automated Production Lines & 100 Stations ---
    # Line configurations: (LineNum, Name, StationCount, Y_Center, BlockSequence)
    lines_config = [
        (1, "LINE 01 – SYNTHETIC AUDI E-TRON MODULE LINE (AI SIM)", 12, 1470,
         ['CONVEYOR_BED', 'CONVEYOR_BED', 'DISPENSING_CELL', 'ROBOT_CELL', 'FASTENING_STATION', 'LASER_WELD_CELL', 'EOL_TEST_BENCH', 'ROBOT_CELL', 'VISION_INSPECTION', 'FASTENING_STATION', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (2, "LINE 02 – SYNTHETIC AUDI BATTERY PACK LINE (AI SIM)", 13, 1280,
         ['CONVEYOR_BED', 'DISPENSING_CELL', 'ROBOT_CELL', 'ROBOT_CELL', 'FASTENING_STATION', 'LASER_WELD_CELL', 'VISION_INSPECTION', 'EOL_TEST_BENCH', 'SERVO_PRESS', 'FASTENING_STATION', 'VISION_INSPECTION', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (3, "LINE 03 – SYNTHETIC AUDI POWERTRAIN LINE (AI SIM)", 12, 1090,
         ['CONVEYOR_BED', 'SERVO_PRESS', 'ROBOT_CELL', 'FASTENING_STATION', 'SERVO_PRESS', 'DISPENSING_CELL', 'EOL_TEST_BENCH', 'VISION_INSPECTION', 'FASTENING_STATION', 'ROBOT_CELL', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (4, "LINE 04 – SYNTHETIC ELECTRONICS SMT PLACEMENT (AI SIM)", 13, 900,
         ['CONVEYOR_BED', 'VISION_INSPECTION', 'ROBOT_CELL', 'DISPENSING_CELL', 'VISION_INSPECTION', 'SERVO_PRESS', 'EOL_TEST_BENCH', 'ROBOT_CELL', 'VISION_INSPECTION', 'FASTENING_STATION', 'CONVEYOR_BED', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (5, "LINE 05 – SYNTHETIC BODY-IN-WHITE ROBOTIC WELDING (AI SIM)", 12, 710,
         ['CONVEYOR_BED', 'ROBOT_CELL', 'LASER_WELD_CELL', 'ROBOT_CELL', 'FASTENING_STATION', 'LASER_WELD_CELL', 'VISION_INSPECTION', 'ROBOT_CELL', 'FASTENING_STATION', 'ROBOT_CELL', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (6, "LINE 06 – SYNTHETIC PRECISION PRESS FIT (AI SIM)", 12, 520,
         ['CONVEYOR_BED', 'SERVO_PRESS', 'SERVO_PRESS', 'ROBOT_CELL', 'FASTENING_STATION', 'SERVO_PRESS', 'VISION_INSPECTION', 'EOL_TEST_BENCH', 'SERVO_PRESS', 'ROBOT_CELL', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (7, "LINE 07 – SYNTHETIC OPTICAL QUALITY METROLOGY (AI SIM)", 13, 330,
         ['CONVEYOR_BED', 'VISION_INSPECTION', 'VISION_INSPECTION', 'ROBOT_CELL', 'VISION_INSPECTION', 'EOL_TEST_BENCH', 'VISION_INSPECTION', 'ROBOT_CELL', 'VISION_INSPECTION', 'CONVEYOR_BED', 'CONVEYOR_BED', 'CONVEYOR_BED', 'CONVEYOR_BED']),
        (8, "LINE 08 – SYNTHETIC END-OF-LINE VEHICLE INTEGRATION (AI SIM)", 13, 140,
         ['CONVEYOR_BED', 'EOL_TEST_BENCH', 'EOL_TEST_BENCH', 'ROBOT_CELL', 'VISION_INSPECTION', 'EOL_TEST_BENCH', 'FASTENING_STATION', 'EOL_TEST_BENCH', 'ROBOT_CELL', 'VISION_INSPECTION', 'CONVEYOR_BED', 'CONVEYOR_BED', 'CONVEYOR_BED'])
    ]

    total_stations_generated = 0

    for line_num, line_title, st_count, y_pos, blk_seq in lines_config:
        # Draw Line Header Banner
        msp.add_text(line_title, height=10).set_placement((60, y_pos + 60), align=TextEntityAlignment.LEFT).dxf.layer = 'TEXT'

        # Main Conveyor Loop Rail (x: 80 to 1250)
        # Upper track
        msp.add_line((80, y_pos + 20), (1240, y_pos + 20), dxfattribs={'layer': 'CONVEYORS'})
        # Lower return track
        msp.add_line((80, y_pos - 20), (1240, y_pos - 20), dxfattribs={'layer': 'CONVEYORS'})
        # Return turn loops at ends
        msp.add_arc((80, y_pos), 20, 90, 270, dxfattribs={'layer': 'CONVEYORS'})
        msp.add_arc((1240, y_pos), 20, 270, 90, dxfattribs={'layer': 'CONVEYORS'})

        # Directional Flow Arrows
        for arrow_x in [300, 600, 900]:
            msp.add_line((arrow_x, y_pos + 20), (arrow_x + 15, y_pos + 20), dxfattribs={'layer': 'CONVEYORS'})
            msp.add_line((arrow_x + 15, y_pos + 20), (arrow_x + 10, y_pos + 24), dxfattribs={'layer': 'CONVEYORS'})
            msp.add_line((arrow_x + 15, y_pos + 20), (arrow_x + 10, y_pos + 16), dxfattribs={'layer': 'CONVEYORS'})

        # Place the Stations along the conveyor
        x_start = 120
        x_spacing = (1200 - x_start) / (st_count - 1)

        for i in range(st_count):
            op_num = (i + 1) * 10
            st_handle = f"L{line_num:02d}-OP{op_num:03d}"
            st_x = x_start + (i * x_spacing)
            # Alternate stations slightly above/below conveyor for clarity
            st_y = y_pos + (20 if i % 2 == 0 else -20)

            b_name = blk_seq[i % len(blk_seq)]
            ref = msp.add_blockref(b_name, (st_x, st_y))
            # CRITICAL: Set DXF handle directly to station identifier
            ref.dxf.handle = st_handle

            # Station Label
            lbl_y = st_y + (16 if i % 2 == 0 else -18)
            msp.add_text(st_handle, height=4.0).set_placement((st_x, lbl_y), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

            total_stations_generated += 1

    # --- 4. Render High-Bay Warehouse & Spare Parts Storage (x: 1400 - 2300) ---
    # Racks for 550 serialized spare items
    for rack_row in range(7):
        ry = 200 + (rack_row * 200)
        msp.add_text(f"WAREHOUSE AISLE {chr(65 + rack_row)} – SPARE PARTS & BULK STORAGE", height=8).set_placement((1420, ry + 40), align=TextEntityAlignment.LEFT).dxf.layer = 'TEXT'
        for rack_col in range(4):
            rx = 1500 + (rack_col * 200)
            rack_ref = msp.add_blockref('WAREHOUSE_RACK', (rx, ry))
            rack_ref.dxf.handle = f"RACK-{chr(65 + rack_row)}-{rack_col + 1}"

    # Ensure output directory exists
    out_dir = 'frontend/web/public/sample'
    os.makedirs(out_dir, exist_ok=True)
    
    assembly_path = os.path.join(out_dir, 'assembly_line.dxf')
    hall_path = os.path.join(out_dir, 'production_hall.dxf')

    doc.saveas(assembly_path)
    doc.saveas(hall_path)

    print(f"✓ Revamped CAD Plant Layout generated successfully!")
    print(f"✓ Total Stations with linked handles: {total_stations_generated} stations (8 lines)")
    print(f"✓ Output files: {assembly_path} and {hall_path}")
    return total_stations_generated

if __name__ == '__main__':
    build_dxf()
