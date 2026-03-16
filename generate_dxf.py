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
msp.add_lwpolyline([(0, 0), (200, 0), (200, 150), (0, 150), (0, 0)], dxfattribs={'layer': 'WALLS'})
# Inner walls
msp.add_lwpolyline([(60, 0), (60, 50), (200, 50)], dxfattribs={'layer': 'WALLS'})
msp.add_lwpolyline([(140, 50), (140, 150)], dxfattribs={'layer': 'WALLS'})

# Add text labels for areas
msp.add_text("CNC MACHINING").set_placement((30, 25), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("ASSEMBLY LINE A").set_placement((100, 25), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("ASSEMBLY LINE B").set_placement((70, 100), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'
msp.add_text("PACKAGING & QA").set_placement((170, 100), align=TextEntityAlignment.MIDDLE_CENTER).dxf.layer = 'TEXT'

# --- Define Blocks for Reusability ---

# CNC Machine Block
cnc_block = doc.blocks.new(name='CNC_MACHINE')
cnc_block.add_lwpolyline([(-8, -6), (8, -6), (8, 6), (-8, 6), (-8, -6)], dxfattribs={'layer': 'STATIONS'})
cnc_block.add_circle((0, 0), 2, dxfattribs={'layer': 'STATIONS'})
cnc_block.add_text("CNC", height=2).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

# Robot Cell Block (Safety fence + Robot)
robot_cell = doc.blocks.new(name='ROBOT_CELL')
# Fence
robot_cell.add_lwpolyline([(-10, -10), (10, -10), (10, 10), (-10, 10), (-10, -10)], dxfattribs={'layer': 'ROBOT_CELLS', 'linetype': 'DASHED'})
# Base
robot_cell.add_circle((0, 0), 3, dxfattribs={'layer': 'ROBOT_CELLS'})
# Arm
robot_cell.add_line((0, 0), (6, 6), dxfattribs={'layer': 'ROBOT_CELLS'})
robot_cell.add_text("ROBOT", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

# Manual Assembly Station
assembly_st = doc.blocks.new(name='ASSEMBLY_STATION')
assembly_st.add_lwpolyline([(-5, -4), (5, -4), (5, 4), (-5, 4), (-5, -4)], dxfattribs={'layer': 'STATIONS'})
assembly_st.add_text("STATION", height=1.5).set_placement((0, 0), align=TextEntityAlignment.MIDDLE_CENTER)

# Packaging Station
pack_st = doc.blocks.new(name='PACKAGING_STATION')
pack_st.add_lwpolyline([(-6, -6), (6, -6), (6, 6), (-6, 6), (-6, -6)], dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, -6), (6, 6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_line((-6, 6), (6, -6), dxfattribs={'layer': 'STATIONS'})
pack_st.add_text("PACK", height=1.5).set_placement((0, -8), align=TextEntityAlignment.MIDDLE_CENTER)

# --- Insert Blocks into Modelspace ---

# CNC Machines
for i in range(3):
    msp.add_blockref('CNC_MACHINE', (15 + i*20, 40))

# Assembly Line A (Conveyor + Stations + Robots)
msp.add_lwpolyline([(65, 20), (195, 20)], dxfattribs={'layer': 'CONVEYORS'})
msp.add_lwpolyline([(65, 22), (195, 22)], dxfattribs={'layer': 'CONVEYORS'})

msp.add_blockref('ROBOT_CELL', (80, 35))
msp.add_blockref('ASSEMBLY_STATION', (110, 30))
msp.add_blockref('ROBOT_CELL', (140, 35))
msp.add_blockref('ASSEMBLY_STATION', (170, 30))

# Assembly Line B
msp.add_lwpolyline([(10, 60), (130, 60)], dxfattribs={'layer': 'CONVEYORS'})
msp.add_lwpolyline([(10, 62), (130, 62)], dxfattribs={'layer': 'CONVEYORS'})

msp.add_blockref('ASSEMBLY_STATION', (30, 70))
msp.add_blockref('ASSEMBLY_STATION', (60, 70))
msp.add_blockref('ROBOT_CELL', (90, 75))
msp.add_blockref('ASSEMBLY_STATION', (120, 70))

# Packaging Area
msp.add_blockref('PACKAGING_STATION', (160, 70))
msp.add_blockref('PACKAGING_STATION', (185, 70))
msp.add_blockref('PACKAGING_STATION', (160, 110))
msp.add_blockref('PACKAGING_STATION', (185, 110))


# Ensure output directory exists
os.makedirs('frontend/nuxt-app/public/sample', exist_ok=True)
doc.saveas('frontend/nuxt-app/public/sample/assembly_line.dxf')
print("Realistic DXF created successfully!")
