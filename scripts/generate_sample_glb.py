#!/usr/bin/env python3
"""
Generates a valid, self-contained glTF 2.0 Binary (.glb) model of an industrial automation workstation.
Each mechanical component is an independent named node in the scene graph matching Heimdall slot IDs:
- FRAME-BASE-01
- CONVEYOR-01
- GANTRY-X-01
- GANTRY-Z-01
- TOOL-HEAD-01
- VISION-CAM-01
- PANEL-STATUS-01
"""

import struct
import json
import os

def create_box_mesh(min_x, min_y, min_z, max_x, max_y, max_z):
    """
    Creates vertex positions, normals, and triangle indices for an axis-aligned box.
    Returns: (positions_bytes, normals_bytes, indices_bytes, vertex_count, index_count, min_pt, max_pt)
    """
    # 6 faces * 4 vertices = 24 vertices
    # Normals: +Z, -Z, +X, -X, +Y, -Y
    faces = [
        # +Z face (front)
        ([ (min_x, min_y, max_z), (max_x, min_y, max_z), (max_x, max_y, max_z), (min_x, max_y, max_z) ], (0, 0, 1)),
        # -Z face (back)
        ([ (max_x, min_y, min_z), (min_x, min_y, min_z), (min_x, max_y, min_z), (max_x, max_y, min_z) ], (0, 0, -1)),
        # +X face (right)
        ([ (max_x, min_y, max_z), (max_x, min_y, min_z), (max_x, max_y, min_z), (max_x, max_y, max_z) ], (1, 0, 0)),
        # -X face (left)
        ([ (min_x, min_y, min_z), (min_x, min_y, max_z), (min_x, max_y, max_z), (min_x, max_y, min_z) ], (-1, 0, 0)),
        # +Y face (top)
        ([ (min_x, max_y, max_z), (max_x, max_y, max_z), (max_x, max_y, min_z), (min_x, max_y, min_z) ], (0, 1, 0)),
        # -Y face (bottom)
        ([ (min_x, min_y, min_z), (max_x, min_y, min_z), (max_x, min_y, max_z), (min_x, min_y, max_z) ], (0, -1, 0)),
    ]

    positions = []
    normals = []
    indices = []
    
    idx_offset = 0
    for verts, norm in faces:
        for v in verts:
            positions.extend(v)
            normals.extend(norm)
        # Two triangles for the quad: (0, 1, 2) and (0, 2, 3)
        indices.extend([idx_offset, idx_offset + 1, idx_offset + 2, idx_offset, idx_offset + 2, idx_offset + 3])
        idx_offset += 4

    pos_bytes = struct.pack(f'{len(positions)}f', *positions)
    norm_bytes = struct.pack(f'{len(normals)}f', *normals)
    idx_bytes = struct.pack(f'{len(indices)}H', *indices)

    min_pt = [min_x, min_y, min_z]
    max_pt = [max_x, max_y, max_z]

    return pos_bytes, norm_bytes, idx_bytes, 24, 36, min_pt, max_pt

def merge_boxes(box_defs):
    """
    Merges multiple box definitions into a single mesh.
    box_defs is a list of tuples: (min_x, min_y, min_z, max_x, max_y, max_z)
    """
    all_pos = bytearray()
    all_norm = bytearray()
    all_idx = bytearray()
    total_verts = 0
    total_indices = 0
    
    g_min = [float('inf'), float('inf'), float('inf')]
    g_max = [float('-inf'), float('-inf'), float('-inf')]

    for b in box_defs:
        pos, norm, idx, v_cnt, i_cnt, b_min, b_max = create_box_mesh(*b)
        # Shift indices by current vertex count
        raw_idx = struct.unpack(f'{i_cnt}H', idx)
        shifted_idx = [i + total_verts for i in raw_idx]
        shifted_idx_bytes = struct.pack(f'{i_cnt}H', *shifted_idx)

        all_pos.extend(pos)
        all_norm.extend(norm)
        all_idx.extend(shifted_idx_bytes)

        total_verts += v_cnt
        total_indices += i_cnt

        for c in range(3):
            g_min[c] = min(g_min[c], b_min[c])
            g_max[c] = max(g_max[c], b_max[c])

    return bytes(all_pos), bytes(all_norm), bytes(all_idx), total_verts, total_indices, g_min, g_max

def build_station_glb(output_path):
    # Define components with their composite shapes and PBR colors
    components = [
        {
            "name": "FRAME-BASE-01",
            "boxes": [
                # 4 Base legs
                (-0.6, 0.0, -0.4, -0.52, 0.8, -0.32),
                (0.52, 0.0, -0.4, 0.6, 0.8, -0.32),
                (-0.6, 0.0, 0.32, -0.52, 0.8, 0.4),
                (0.52, 0.0, 0.32, 0.6, 0.8, 0.4),
                # Tabletop plate
                (-0.65, 0.8, -0.45, 0.65, 0.85, 0.45),
                # Vertical gantry support columns
                (-0.55, 0.85, -0.25, -0.47, 1.45, -0.17),
                (0.47, 0.85, -0.25, 0.55, 1.45, -0.17)
            ],
            "color": [0.22, 0.25, 0.28, 1.0], # Dark industrial slate
            "metallic": 0.4,
            "roughness": 0.6
        },
        {
            "name": "CONVEYOR-01",
            "boxes": [
                # Left belt rail
                (-0.65, 0.85, -0.12, 0.65, 0.89, -0.08),
                # Right belt rail
                (-0.65, 0.85, 0.08, 0.65, 0.89, 0.12),
                # PCB carrier pallet in workstation center
                (-0.15, 0.88, -0.07, 0.15, 0.90, 0.07)
            ],
            "color": [0.12, 0.48, 0.42, 1.0], # Industrial teal green
            "metallic": 0.2,
            "roughness": 0.7
        },
        {
            "name": "GANTRY-X-01",
            "boxes": [
                # Horizontal traverse gantry beam
                (-0.55, 1.35, -0.23, 0.55, 1.45, -0.13),
                # Linear carriage slider
                (-0.12, 1.32, -0.24, 0.12, 1.48, -0.08)
            ],
            "color": [0.75, 0.78, 0.82, 1.0], # Anodized silver
            "metallic": 0.8,
            "roughness": 0.3
        },
        {
            "name": "GANTRY-Z-01",
            "boxes": [
                # Vertical precision slide axis
                (-0.06, 1.08, -0.12, 0.06, 1.36, -0.04)
            ],
            "color": [0.35, 0.40, 0.45, 1.0], # Dark chrome
            "metallic": 0.7,
            "roughness": 0.35
        },
        {
            "name": "TOOL-HEAD-01",
            "boxes": [
                # Dispenser valve body / Spindle housing
                (-0.05, 0.96, -0.08, 0.05, 1.08, 0.02),
                # Dispenser nozzle / Screwdriver bit
                (-0.015, 0.90, -0.04, 0.015, 0.96, -0.01)
            ],
            "color": [0.95, 0.48, 0.08, 1.0], # Signal safety orange
            "metallic": 0.5,
            "roughness": 0.4
        },
        {
            "name": "VISION-CAM-01",
            "boxes": [
                # Camera sensor body
                (0.07, 1.02, -0.06, 0.15, 1.12, 0.02),
                # Ring light illuminator / lens
                (0.08, 0.96, -0.05, 0.14, 1.02, 0.01)
            ],
            "color": [0.18, 0.42, 0.85, 1.0], # Vision system blue
            "metallic": 0.3,
            "roughness": 0.5
        },
        {
            "name": "PANEL-STATUS-01",
            "boxes": [
                # Mounting pole
                (0.55, 0.85, -0.38, 0.58, 1.25, -0.35),
                # Red light segment
                (0.54, 1.25, -0.39, 0.59, 1.28, -0.34),
                # Yellow light segment
                (0.54, 1.28, -0.39, 0.59, 1.31, -0.34),
                # Green light segment
                (0.54, 1.31, -0.39, 0.59, 1.34, -0.34)
            ],
            "color": [0.25, 0.85, 0.35, 1.0], # Status beacon bright green
            "metallic": 0.1,
            "roughness": 0.2
        }
    ]

    bin_buffer = bytearray()
    nodes = []
    meshes = []
    materials = []
    accessors = []
    buffer_views = []

    # 1 bufferView for positions, 1 for normals, 1 for indices per component
    for idx, comp in enumerate(components):
        pos_b, norm_b, idx_b, v_cnt, i_cnt, b_min, b_max = merge_boxes(comp["boxes"])

        # Align to 4 bytes
        def align_4(buf):
            pad = (4 - (len(buf) % 4)) % 4
            buf.extend(b'\x00' * pad)

        align_4(bin_buffer)

        # BufferView 0: Positions
        pos_offset = len(bin_buffer)
        bin_buffer.extend(pos_b)
        align_4(bin_buffer)
        pos_len = len(bin_buffer) - pos_offset
        bv_pos_idx = len(buffer_views)
        buffer_views.append({
            "buffer": 0,
            "byteOffset": pos_offset,
            "byteLength": len(pos_b),
            "target": 34962 # ARRAY_BUFFER
        })
        acc_pos_idx = len(accessors)
        accessors.append({
            "bufferView": bv_pos_idx,
            "byteOffset": 0,
            "componentType": 5126, # FLOAT
            "count": v_cnt,
            "type": "VEC3",
            "max": b_max,
            "min": b_min
        })

        # BufferView 1: Normals
        norm_offset = len(bin_buffer)
        bin_buffer.extend(norm_b)
        align_4(bin_buffer)
        bv_norm_idx = len(buffer_views)
        buffer_views.append({
            "buffer": 0,
            "byteOffset": norm_offset,
            "byteLength": len(norm_b),
            "target": 34962 # ARRAY_BUFFER
        })
        acc_norm_idx = len(accessors)
        accessors.append({
            "bufferView": bv_norm_idx,
            "byteOffset": 0,
            "componentType": 5126, # FLOAT
            "count": v_cnt,
            "type": "VEC3",
            "max": [1.0, 1.0, 1.0],
            "min": [-1.0, -1.0, -1.0]
        })

        # BufferView 2: Indices
        idx_offset = len(bin_buffer)
        bin_buffer.extend(idx_b)
        align_4(bin_buffer)
        bv_idx_idx = len(buffer_views)
        buffer_views.append({
            "buffer": 0,
            "byteOffset": idx_offset,
            "byteLength": len(idx_b),
            "target": 34963 # ELEMENT_ARRAY_BUFFER
        })
        acc_idx_idx = len(accessors)
        accessors.append({
            "bufferView": bv_idx_idx,
            "byteOffset": 0,
            "componentType": 5123, # UNSIGNED_SHORT
            "count": i_cnt,
            "type": "SCALAR",
            "max": [v_cnt - 1],
            "min": [0]
        })

        # Material
        mat_idx = len(materials)
        materials.append({
            "name": f"mat_{comp['name']}",
            "pbrMetallicRoughness": {
                "baseColorFactor": comp["color"],
                "metallicFactor": comp["metallic"],
                "roughnessFactor": comp["roughness"]
            }
        })

        # Mesh
        mesh_idx = len(meshes)
        meshes.append({
            "name": f"mesh_{comp['name']}",
            "primitives": [
                {
                    "attributes": {
                        "POSITION": acc_pos_idx,
                        "NORMAL": acc_norm_idx
                    },
                    "indices": acc_idx_idx,
                    "material": mat_idx
                }
            ]
        })

        # Node
        node_idx = len(nodes)
        nodes.append({
            "name": comp["name"],
            "mesh": mesh_idx
        })

    gltf_dict = {
        "asset": {
            "version": "2.0",
            "generator": "Heimdall Industrial CAD Pipeline v1.0"
        },
        "scene": 0,
        "scenes": [
            {
                "name": "WorkstationScene",
                "nodes": list(range(len(nodes)))
            }
        ],
        "nodes": nodes,
        "meshes": meshes,
        "materials": materials,
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [
            {
                "byteLength": len(bin_buffer)
            }
        ]
    }

    json_str = json.dumps(gltf_dict, separators=(',', ':'))
    json_bytes = json_str.encode('utf-8')
    # Pad JSON chunk to 4-byte boundary with spaces (0x20)
    json_pad = (4 - (len(json_bytes) % 4)) % 4
    json_bytes += b' ' * json_pad

    # Pad BIN chunk to 4-byte boundary with nulls (0x00)
    bin_pad = (4 - (len(bin_buffer) % 4)) % 4
    bin_buffer += b'\x00' * bin_pad

    # GLB Header: 12 bytes
    magic = 0x46546C67 # "glTF"
    version = 2
    total_length = 12 + 8 + len(json_bytes) + 8 + len(bin_buffer)

    header = struct.pack('<III', magic, version, total_length)
    json_chunk_header = struct.pack('<II', len(json_bytes), 0x4E4F534A) # JSON
    bin_chunk_header = struct.pack('<II', len(bin_buffer), 0x004E4942) # BIN

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, 'wb') as f:
        f.write(header)
        f.write(json_chunk_header)
        f.write(json_bytes)
        f.write(bin_chunk_header)
        f.write(bin_buffer)

    print(f"Generated GLB successfully: {output_path} ({total_length} bytes)")

if __name__ == "__main__":
    out = "src/frontend/web/public/sample/station_digital_twin.glb"
    build_station_glb(out)
