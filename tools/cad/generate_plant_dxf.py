import ezdxf
from ezdxf import colors
from ezdxf.enums import TextEntityAlignment
import os

doc = ezdxf.new('R2010')
msp = doc.modelspace()

# Create Layers
doc.layers.add("WALLS", color=colors.CYAN)
doc.layers.add("CONVEYORS", color=colors.YELLOW)
doc.layers.add("ROBOT_CELLS", color=colors.MAGENTA)
doc.layers.add("STATIONS", color=colors.GREEN)
doc.layers.add("TEXT", color=colors.WHITE)

# 1. Main Plant Outer Perimeter (1200 x 800 units)
msp.add_lwpolyline([(0, 0), (1200, 0), (1200, 800), (0, 800), (0, 0)], dxfattribs={'layer': 'WALLS'})

# 2. Four Main Production Floors Division Walls
# Floor A (Top Left: 0-600, 400-800)
# Floor B (Top Right: 600-1200, 400-800)
# Floor C (Bottom Left: 0-600, 0-400)
# Floor D (Bottom Right: 600-1200, 0-400)
msp.add_lwpolyline([(600, 0), (600, 800)], dxfattribs={'layer': 'WALLS'})
msp.add_lwpolyline([(0, 400), (1200, 400)], dxfattribs={'layer': 'WALLS'})

# Floor Labels
msp.add_text("PRODUCTION FLOOR A - BODY & PRESS", height=12).set_placement((300, 770), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("PRODUCTION FLOOR B - POWERTRAIN & BATTERY", height=12).set_placement((900, 770), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("PRODUCTION FLOOR C - EOL TESTING & PACKAGING", height=12).set_placement((300, 370), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("PRODUCTION FLOOR D - CHEMICAL & INTEGRATION", height=12).set_placement((900, 370), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

# --- Define Blocks for Reusability ---
cnc_block = doc.blocks.new(name='CNC_MACHINE')
cnc_block.add_lwpolyline([(-8, -6), (8, -6), (8, 6), (-8, 6), (-8, -6)], dxfattribs={'layer': 'STATIONS'})
cnc_block.add_circle((0, 0), 2, dxfattribs={'layer': 'STATIONS'})
cnc_block.add_text("CNC", height=2).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

robot_cell = doc.blocks.new(name='ROBOT_CELL')
robot_cell.add_lwpolyline([(-10, -10), (10, -10), (10, 10), (-10, 10), (-10, -10)], dxfattribs={'layer': 'ROBOT_CELLS', 'linetype': 'DASHED'})
robot_cell.add_circle((0, 0), 3, dxfattribs={'layer': 'ROBOT_CELLS'})
robot_cell.add_line((0, 0), (6, 6), dxfattribs={'layer': 'ROBOT_CELLS'})
robot_cell.add_text("ROBOT", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

assembly_st = doc.blocks.new(name='ASSEMBLY_STATION')
assembly_st.add_lwpolyline([(-6, -5), (6, -5), (6, 5), (-6, 5), (-6, -5)], dxfattribs={'layer': 'STATIONS'})
assembly_st.add_text("STATION", height=1.5).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

pack_st = doc.blocks.new(name='PACKAGING_STATION')
pack_st.add_lwpolyline([(-6, -6), (6, -6), (6, 6), (-6, 6), (-6, -6)], dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, -6), (6, 6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, 6), (6, -6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_text("PACK", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

# Chemical Tanks in Floor D
for i, (tx, ty) in enumerate([(750, 200), (850, 200), (1000, 200), (1100, 200)]):
    tank = msp.add_circle((tx, ty), 16, dxfattribs={'layer': 'STATIONS'})
    tank.dxf.handle = f"C-TANK-{i+1}" if i > 0 else "C-TANK-4"
    msp.add_text(f"TANK {i+1}", height=4).set_placement((tx, ty), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

# Specific Seed Station Placements
# Floor A (Body & Press)
al1 = msp.add_blockref('ROBOT_CELL', (120, 650))
al1.dxf.handle = 'H-AL1'
ws5 = msp.add_blockref('ASSEMBLY_STATION', (250, 650))
ws5.dxf.handle = 'H-WS5'
cnc_mc = msp.add_blockref('CNC_MACHINE', (400, 650))
cnc_mc.dxf.handle = 'CNC-MC-12'

# Floor B (Powertrain & Battery)
q_cell = msp.add_blockref('ASSEMBLY_STATION', (750, 650))
q_cell.dxf.handle = 'Q-CELL-01'
p_cell = msp.add_blockref('ROBOT_CELL', (900, 650))
p_cell.dxf.handle = 'P-CELL-02'
t_cell = msp.add_blockref('CNC_MACHINE', (1050, 650))
t_cell.dxf.handle = 'T-CELL-01'

# Floor C (EOL Testing & Packaging)
log_a = msp.add_blockref('ROBOT_CELL', (150, 250))
log_a.dxf.handle = 'L-SORT-A'
pack_b = msp.add_blockref('PACKAGING_STATION', (300, 250))
pack_b.dxf.handle = 'P-LINE-B'
pack_c = msp.add_blockref('PACKAGING_STATION', (450, 250))
pack_c.dxf.handle = 'P-LINE-C'

# Layout production conveyor lines & additional interactive stations
block_types = ['ROBOT_CELL', 'ASSEMBLY_STATION', 'CNC_MACHINE', 'PACKAGING_STATION']

# Conveyor tracks across the floors
for floor_x, floor_y in [(50, 520), (50, 650), (650, 520), (650, 650), (50, 150), (50, 250), (650, 100), (650, 300)]:
    msp.add_line((floor_x, floor_y), (floor_x + 500, floor_y), dxfattribs={'layer': 'CONVEYORS', 'linetype': 'CENTER'})
    for col in range(6):
        x = floor_x + (col * 80) + 40
        y = floor_y
        # Skip if near specific seed handles
        if (abs(x - 120) < 30 and abs(y - 650) < 30) or (abs(x - 250) < 30 and abs(y - 650) < 30) or \
           (abs(x - 400) < 30 and abs(y - 650) < 30) or (abs(x - 750) < 30 and abs(y - 650) < 30) or \
           (abs(x - 900) < 30 and abs(y - 650) < 30) or (abs(x - 1050) < 30 and abs(y - 650) < 30) or \
           (abs(x - 150) < 30 and abs(y - 250) < 30) or (abs(x - 300) < 30 and abs(y - 250) < 30) or \
           (abs(x - 450) < 30 and abs(y - 250) < 30):
            continue
        
        btype = block_types[(col + int(floor_y)) % len(block_types)]
        ref = msp.add_blockref(btype, (x, y))
        ref.dxf.handle = f"ST-{int(floor_x/100)}-{int(floor_y/100)}-{col+1}"

# Ensure output directory exists
os.makedirs('frontend/heimdall-web-frontend/public/sample', exist_ok=True)
doc.saveas('frontend/heimdall-web-frontend/public/sample/assembly_line.dxf')
print("Multi-floor Enterprise Plant DXF generated successfully with realistic conveyor lines, tanks, and stations!")
