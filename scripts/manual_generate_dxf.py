import json
import os
import ezdxf
from ezdxf.enums import TextEntityAlignment
from ezdxf import colors

# --- CONFIGURATION CONSTANTS ---
STATION_WIDTH = 12
STATION_HEIGHT = 10
STATION_SPACING = 30 # Increased for less crowding
LINE_H_PADDING = 30
LINE_V_PADDING = 40
HALL_LINE_SCALE = 0.6 # Scaled down for better fit in hall

def create_station_blocks(doc):
    """Defines specialized blocks for different industrial station types, centered on (0,0)."""
    
    w = STATION_WIDTH
    h = STATION_HEIGHT
    cx, cy = w/2, h/2

    def add_safety_fence(blk):
        # Centered dashed yellow fence
        blk.add_lwpolyline([(-1, -1), (w+1, -1), (w+1, h+1), (-1, h+1), (-1, -1)], 
                           close=True, dxfattribs={'layer': 'SAFETY', 'linetype': 'DASHED', 'color': colors.YELLOW})

    # --- 2. Mechanical Station ---
    if 'TYPE_MECHANICAL' not in doc.blocks:
        mech_blk = doc.blocks.new(name='TYPE_MECHANICAL')
        add_safety_fence(mech_blk)
        mech_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.CYAN})
        mech_blk.add_lwpolyline([(2, 2), (10, 2), (10, 8), (2, 8), (2, 2)], close=True, dxfattribs={'layer': 'STATIONS', 'color': 8})
        mech_blk.add_line((0,0), (w,h), dxfattribs={'layer': 'STATIONS', 'color': 8})
        mech_blk.add_line((0,h), (w,0), dxfattribs={'layer': 'STATIONS', 'color': 8})

    # --- 3. Screwing Station ---
    if 'TYPE_SCREWING' not in doc.blocks:
        screw_blk = doc.blocks.new(name='TYPE_SCREWING')
        add_safety_fence(screw_blk)
        screw_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.GREEN})
        screw_blk.add_lwpolyline([(6, 10), (6, 6), (9, 6)], dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})
        screw_blk.add_circle((9, 6), 1, dxfattribs={'layer': 'STATIONS', 'color': colors.YELLOW})

    # --- 4. Vision Station ---
    if 'TYPE_VISION' not in doc.blocks:
        vision_blk = doc.blocks.new(name='TYPE_VISION')
        add_safety_fence(vision_blk)
        vision_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.MAGENTA})
        vision_blk.add_circle((6, 5), 2, dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})
        vision_blk.add_circle((6, 5), 0.5, dxfattribs={'layer': 'STATIONS', 'color': colors.CYAN})
        vision_blk.add_line((2, 1), (10, 1), dxfattribs={'layer': 'STATIONS', 'color': colors.YELLOW})
        vision_blk.add_line((2, 9), (10, 9), dxfattribs={'layer': 'STATIONS', 'color': colors.YELLOW})

    # --- 5. Test Station ---
    if 'TYPE_TEST' not in doc.blocks:
        test_blk = doc.blocks.new(name='TYPE_TEST')
        add_safety_fence(test_blk)
        test_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.RED})
        test_blk.add_lwpolyline([(3, 3), (9, 3), (9, 7), (3, 7), (3, 3)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})
        test_blk.add_line((4, 4), (8, 6), dxfattribs={'layer': 'STATIONS', 'color': colors.GREEN})

    # --- 6. Dispensing Station ---
    if 'TYPE_DISPENSING' not in doc.blocks:
        disp_blk = doc.blocks.new(name='TYPE_DISPENSING')
        add_safety_fence(disp_blk)
        disp_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.BLUE})
        disp_blk.add_circle((3, 5), 2, dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})
        disp_blk.add_lwpolyline([(5, 5), (10, 5), (10, 3)], dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})

    # --- 7. Welding Station ---
    if 'TYPE_WELDING' not in doc.blocks:
        weld_blk = doc.blocks.new(name='TYPE_WELDING')
        add_safety_fence(weld_blk)
        weld_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.YELLOW})
        weld_blk.add_lwpolyline([(2, 2), (6, 5), (10, 8)], dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})
        weld_blk.add_circle((10, 8), 0.5, dxfattribs={'layer': 'STATIONS', 'color': colors.RED})

    # --- 8. Packaging Station ---
    if 'TYPE_PACKAGING' not in doc.blocks:
        pack_blk = doc.blocks.new(name='TYPE_PACKAGING')
        add_safety_fence(pack_blk)
        pack_blk.add_lwpolyline([(0, 0), (w, 0), (w, h), (0, h), (0, 0)], close=True, dxfattribs={'layer': 'STATIONS', 'color': 40})
        pack_blk.add_lwpolyline([(4, 4), (8, 4), (8, 8), (4, 8), (4, 4)], close=True, dxfattribs={'layer': 'STATIONS', 'color': colors.WHITE})

def generate_manual_layout(topology_path='seed_data/production_topology.json', output_dir='generated_layouts'):
    if not os.path.exists(topology_path):
        print(f"Error: {topology_path} not found.")
        return

    with open(topology_path, 'r') as f:
        data = json.load(f)

    hall = data['production_hall']
    os.makedirs(output_dir, exist_ok=True)
    os.makedirs('frontend/nuxt-app/public/sample', exist_ok=True)

    print(f"--- Heimdall Clean Integrated DXF Layout Generator ---")

    # --- MASTER HALL SETUP ---
    hall_doc = ezdxf.new('R2010')
    hall_msp = hall_doc.modelspace()
    
    # Layers
    for doc in [hall_doc]:
        doc.layers.add("BUILDING", color=colors.CYAN)
        doc.layers.add("STATIONS", color=colors.GREEN)
        doc.layers.add("SAFETY", color=colors.YELLOW, linetype='DASHED')
        doc.layers.add("CONVEYORS", color=colors.CYAN)
        doc.layers.add("WALKWAYS", color=8)
        doc.layers.add("TEXT", color=colors.WHITE)
        doc.layers.add("FLOOR", color=8)

    create_station_blocks(hall_doc)

    hall_width = 500
    hall_height = 800
    
    # Hall Perimeter
    hall_msp.add_lwpolyline([(0, 0), (hall_width, 0), (hall_width, hall_height), (0, hall_height), (0, 0)], 
                           close=True, dxfattribs={'layer': 'BUILDING', 'lineweight': 70})

    # Main Walkway (Vertical center)
    hall_msp.add_lwpolyline([(hall_width/2 - 20, 0), (hall_width/2 - 20, hall_height)], dxfattribs={'layer': 'WALKWAYS'})
    hall_msp.add_lwpolyline([(hall_width/2 + 20, 0), (hall_width/2 + 20, hall_height)], dxfattribs={'layer': 'WALKWAYS'})
    hall_msp.add_text("CENTRAL LOGISTICS THOROUGHFARE", dxfattribs={'height': 7, 'layer': 'TEXT', 'rotation': 90}).set_placement((hall_width/2, hall_height/2), align=TextEntityAlignment.MIDDLE_CENTER)

    line_y_offset = 60

    for idx, line in enumerate(hall['lines']):
        line_id = line['id']
        line_name = line['name']
        num_stations = len(line['stations'])
        
        # --- LINE DXF SETUP ---
        line_doc = ezdxf.new('R2010')
        line_msp = line_doc.modelspace()
        line_doc.layers.add("STATIONS", color=colors.GREEN)
        line_doc.layers.add("SAFETY", color=colors.YELLOW, linetype='DASHED')
        line_doc.layers.add("CONVEYORS", color=colors.CYAN)
        line_doc.layers.add("TEXT", color=colors.WHITE)
        line_doc.layers.add("FLOOR", color=8)
        create_station_blocks(line_doc)

        # Placement in hall
        is_left = idx % 2 == 0
        x_base_hall = 30 if is_left else hall_width/2 + 50
        
        # Calculate background box size based on actual line width
        line_content_width = (num_stations * STATION_WIDTH) + ((num_stations - 1) * STATION_SPACING)
        floor_width = line_content_width + (LINE_H_PADDING * 2)
        floor_height = STATION_HEIGHT + (LINE_V_PADDING * 2)

        # Draw line background in hall (Centered)
        h_floor_x = x_base_hall
        h_floor_y = line_y_offset
        
        hall_msp.add_lwpolyline([
            (h_floor_x, h_floor_y), 
            (h_floor_x + (floor_width * HALL_LINE_SCALE), h_floor_y), 
            (h_floor_x + (floor_width * HALL_LINE_SCALE), h_floor_y + (floor_height * HALL_LINE_SCALE)), 
            (h_floor_x, h_floor_y + (floor_height * HALL_LINE_SCALE)), 
            (h_floor_x, h_floor_y)
        ], close=True, dxfattribs={'layer': 'FLOOR'})
        
        hall_msp.add_text(line_name, dxfattribs={'height': 6, 'layer': 'TEXT'}).set_placement((h_floor_x, h_floor_y + (floor_height * HALL_LINE_SCALE) + 5))

        # Local line floor
        line_msp.add_lwpolyline([(0, -LINE_V_PADDING), (floor_width, -LINE_V_PADDING), (floor_width, floor_height - LINE_V_PADDING), (0, floor_height - LINE_V_PADDING), (0, -LINE_V_PADDING)], 
                               close=True, dxfattribs={'layer': 'FLOOR'})

        # Build stations
        current_x = LINE_H_PADDING
        for s_idx, station in enumerate(line['stations']):
            s_type = station['type'].upper()
            block_name = f'TYPE_{s_type}'
            if block_name not in hall_doc.blocks: block_name = 'TYPE_MECHANICAL'
            
            # --- LOCAL LINE DXF ---
            line_msp.add_blockref(block_name, (current_x, 0)).dxf.handle = station['id']
            # Name above
            line_msp.add_text(station['name'], dxfattribs={'height': 1.8, 'layer': 'TEXT'}).set_placement((current_x + STATION_WIDTH/2, 14), align=TextEntityAlignment.CENTER)
            # ID below
            line_msp.add_text(station['id'], dxfattribs={'height': 1.2, 'layer': 'TEXT', 'color': 7}).set_placement((current_x + STATION_WIDTH/2, -5), align=TextEntityAlignment.CENTER)
            
            # --- INTEGRATED HALL DXF ---
            h_x = h_floor_x + (current_x * HALL_LINE_SCALE)
            h_y = h_floor_y + (LINE_V_PADDING * HALL_LINE_SCALE)
            
            hall_ins = hall_msp.add_blockref(block_name, (h_x, h_y), dxfattribs={'xscale': HALL_LINE_SCALE, 'yscale': HALL_LINE_SCALE})
            hall_ins.dxf.handle = station['id']
            
            # Label in hall (ID only, offset to avoid collision)
            hall_msp.add_text(station['id'], dxfattribs={'height': 2, 'layer': 'TEXT'}).set_placement((h_x + (STATION_WIDTH * HALL_LINE_SCALE)/2, h_y - 8), align=TextEntityAlignment.CENTER)

            # Conveyors
            if s_idx < num_stations - 1:
                conv_x_start = current_x + STATION_WIDTH
                conv_x_end = conv_x_start + STATION_SPACING
                # Local
                line_msp.add_line((conv_x_start, 3), (conv_x_end, 3), dxfattribs={'layer': 'CONVEYORS'})
                line_msp.add_line((conv_x_start, 7), (conv_x_end, 7), dxfattribs={'layer': 'CONVEYORS'})
                # Hall
                hh_x_start = h_floor_x + (conv_x_start * HALL_LINE_SCALE)
                hh_x_end = h_floor_x + (conv_x_end * HALL_LINE_SCALE)
                hh_y1 = h_y + (3 * HALL_LINE_SCALE)
                hh_y2 = h_y + (7 * HALL_LINE_SCALE)
                hall_msp.add_line((hh_x_start, hh_y1), (hh_x_end, hh_y1), dxfattribs={'layer': 'CONVEYORS'})
                hall_msp.add_line((hh_x_start, hh_y2), (hh_x_end, hh_y2), dxfattribs={'layer': 'CONVEYORS'})

            current_x += STATION_WIDTH + STATION_SPACING

        # Save Line DXF
        line_doc.saveas(f"frontend/nuxt-app/public/sample/{line_id}.dxf")
        print(f"  [+] Line: {line_id}")

        if not is_left:
            line_y_offset += 140 # Vertical spacing between line rows in hall

    # Hall zones
    hall_msp.add_text("INBOUND SHIPPING", dxfattribs={'height': 15, 'layer': 'TEXT'}).set_placement((hall_width/2, 20), align=TextEntityAlignment.MIDDLE_CENTER)
    hall_msp.add_text("TECHNICAL LABS", dxfattribs={'height': 15, 'layer': 'TEXT'}).set_placement((hall_width/2, hall_height - 30), align=TextEntityAlignment.MIDDLE_CENTER)

    hall_doc.saveas(f"frontend/nuxt-app/public/sample/production_hall.dxf")
    print(f"  [+] Integrated Master Hall Generated.")

if __name__ == "__main__":
    generate_manual_layout()
