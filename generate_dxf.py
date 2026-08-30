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

# Draw building walls
msp.add_lwpolyline([(0, 0), (250, 0), (250, 180), (0, 180), (0, 0)], dxfattribs={'layer': 'WALLS'})
# Inner walls
msp.add_lwpolyline([(80, 0), (80, 60), (250, 60)], dxfattribs={'layer': 'WALLS'})
msp.add_lwpolyline([(160, 60), (160, 180)], dxfattribs={'layer': 'WALLS'})

# Add text labels for areas matching seed data logic
msp.add_text("ASSEMBLY HALL").set_placement((40, 30), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("LOGISTICS & PACKAGING").set_placement((165, 30), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("QUALITY & SPECIALTY").set_placement((80, 120), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("CNC MACHINING").set_placement((205, 120), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

# --- Define Blocks for Reusability ---

# CNC Machine Block
cnc_block = doc.blocks.new(name='CNC_MACHINE')
cnc_block.add_lwpolyline([(-8, -6), (8, -6), (8, 6), (-8, 6), (-8, -6)], dxfattribs={'layer': 'STATIONS'})
cnc_block.add_circle((0, 0), 2, dxfattribs={'layer': 'STATIONS'})
cnc_block.add_text("CNC", height=2).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

# Robot Cell Block (Safety fence + Robot)
robot_cell = doc.blocks.new(name='ROBOT_CELL')
robot_cell.add_lwpolyline([(-10, -10), (10, -10), (10, 10), (-10, 10), (-10, -10)], dxfattribs={'layer': 'ROBOT_CELLS', 'linetype': 'DASHED'})
robot_cell.add_circle((0, 0), 3, dxfattribs={'layer': 'ROBOT_CELLS'})
robot_cell.add_line((0, 0), (6, 6), dxfattribs={'layer': 'ROBOT_CELLS'})
robot_cell.add_text("ROBOT", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

# Assembly Station
assembly_st = doc.blocks.new(name='ASSEMBLY_STATION')
assembly_st.add_lwpolyline([(-5, -4), (5, -4), (5, 4), (-5, 4), (-5, -4)], dxfattribs={'layer': 'STATIONS'})
assembly_st.add_text("STATION", height=1.5).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

# Packaging Station
pack_st = doc.blocks.new(name='PACKAGING_STATION')
pack_st.add_lwpolyline([(-6, -6), (6, -6), (6, 6), (-6, 6), (-6, -6)], dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, -6), (6, 6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, 6), (6, -6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_text("PACK", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

# --- Insert Blocks into Modelspace with specific handles from seed data ---

# ASSEMBLY HALL
# Assembly Line 1 (handle: H-AL1)
al1 = msp.add_blockref('ROBOT_CELL', (40, 45))
al1.dxf.handle = 'H-AL1'

# Welding Station 5 (handle: H-WS5)
ws5 = msp.add_blockref('ASSEMBLY_STATION', (20, 15))
ws5.dxf.handle = 'H-WS5'

# LOGISTICS & PACKAGING
# Logistics Sorting System (handle: L-SORT-A)
log_a = msp.add_blockref('ROBOT_CELL', (130, 30))
log_a.dxf.handle = 'L-SORT-A'

# Packaging Line B (handle: P-LINE-B)
pack_b = msp.add_blockref('PACKAGING_STATION', (200, 30))
pack_b.dxf.handle = 'P-LINE-B'

# QUALITY & SPECIALTY
# Quality Inspection Cell (handle: Q-CELL-01)
q_cell = msp.add_blockref('ASSEMBLY_STATION', (40, 140))
q_cell.dxf.handle = 'Q-CELL-01'

# Chemical Mixing Tank (handle: C-TANK-4)
tank_4 = msp.add_circle((120, 140), 10, dxfattribs={'layer': 'STATIONS'})
tank_4.dxf.handle = 'C-TANK-4'
msp.add_text("TANK 4", height=2).set_placement((120, 140), align=TextEntityAlignment.MIDDLE_CENTER)

# MACHINING
# CNC Milling Center (handle: CNC-MC-12)
cnc_12 = msp.add_blockref('CNC_MACHINE', (210, 140))
cnc_12.dxf.handle = 'CNC-MC-12'

# Ensure output directory exists
os.makedirs('frontend/nuxt-app/public/sample', exist_ok=True)
doc.saveas('frontend/nuxt-app/public/sample/assembly_line.dxf')
print("Realistic factory plant DXF generated successfully!")
