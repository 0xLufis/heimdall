import { describe, it, expect } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'
import type { EplanCadReference, StationInstalledPartDto } from '~/types/inventory'

describe('3D CAD Digital Twin & Component Identity Linking Suite', () => {
  const glbPath = path.resolve(__dirname, '../../../src/frontend/web/public/sample/station_digital_twin.glb')

  describe('Sample 3D Industrial Station GLB Asset', () => {
    it('exists and has a valid glTF 2.0 binary header', () => {
      expect(fs.existsSync(glbPath)).toBe(true)
      const buffer = fs.readFileSync(glbPath)
      expect(buffer.length).toBeGreaterThan(1000)

      // 12-byte header: magic (0x46546C67), version (2), length
      const magic = buffer.readUInt32LE(0)
      const version = buffer.readUInt32LE(4)
      const length = buffer.readUInt32LE(8)

      expect(magic).toBe(0x46546C67) // 'glTF'
      expect(version).toBe(2)
      expect(length).toBe(buffer.length)
    })

    it('contains all 7 standard industrial component nodes and meshes matching Heimdall slot IDs', () => {
      const buffer = fs.readFileSync(glbPath)
      const jsonChunkLen = buffer.readUInt32LE(12)
      const jsonChunkType = buffer.readUInt32LE(16)
      expect(jsonChunkType).toBe(0x4E4F534A) // 'JSON'

      const jsonStr = buffer.toString('utf8', 20, 20 + jsonChunkLen)
      const gltf = JSON.parse(jsonStr)

      const expectedSlots = [
        'FRAME-BASE-01',
        'CONVEYOR-01',
        'GANTRY-X-01',
        'GANTRY-Z-01',
        'TOOL-HEAD-01',
        'VISION-CAM-01',
        'PANEL-STATUS-01'
      ]

      const nodeNames = gltf.nodes.map((n: any) => n.name)
      for (const slot of expectedSlots) {
        expect(nodeNames).toContain(slot)
      }

      const meshNames = gltf.meshes.map((m: any) => m.name)
      expect(meshNames.length).toBe(7)
      for (const slot of expectedSlots) {
        expect(meshNames).toContain(`mesh_${slot}`)
      }
    })
  })

  describe('Component Slot to CAD Node Path Matching', () => {
    const sampleParts: StationInstalledPartDto[] = [
      {
        id: 'prt-01',
        name: 'Kinematic Servo Axis',
        cad3dNodePath: 'GANTRY-X-01',
        functionalSlotIdentifier: 'GANTRY-X-01',
        equipmentStatus: 'InMachine',
        technology: 'Kinematics',
        itemType: 'HardwareComponent'
      },
      {
        id: 'prt-02',
        name: 'Piezo Jetting Valve',
        cad3dNodePath: 'TOOL-HEAD-01',
        functionalSlotIdentifier: 'TOOL-HEAD-01',
        equipmentStatus: 'InMachine',
        technology: 'Dispensing',
        itemType: 'HardwareComponent'
      },
      {
        id: 'prt-03',
        name: 'Cognex Vision Camera',
        cad3dNodePath: 'VISION-CAM-01',
        functionalSlotIdentifier: 'VISION-CAM-01',
        equipmentStatus: 'active',
        technology: 'Optics',
        itemType: 'HardwareComponent'
      }
    ]

    it('matches exact and normalized node identifiers', () => {
      const matchSlot = (targetNode: string) => {
        return sampleParts.find(
          p =>
            p.cad3dNodePath?.toUpperCase() === targetNode.toUpperCase() ||
            p.functionalSlotIdentifier?.toUpperCase() === targetNode.toUpperCase()
        )
      }

      expect(matchSlot('GANTRY-X-01')?.name).toBe('Kinematic Servo Axis')
      expect(matchSlot('gantry-x-01')?.name).toBe('Kinematic Servo Axis')
      expect(matchSlot('TOOL-HEAD-01')?.name).toBe('Piezo Jetting Valve')
      expect(matchSlot('VISION-CAM-01')?.name).toBe('Cognex Vision Camera')
      expect(matchSlot('UNKNOWN-NODE')).toBeUndefined()
    })
  })

  describe('EplanCadReference 3D Model Property Typing', () => {
    it('supports 3D transform, bounding box, and model URL', () => {
      const cadRef: EplanCadReference = {
        deviceTag: '=LINE1+OP20-M1',
        cadHandle: '3B4F',
        model3dUrl: '/sample/station_digital_twin.glb',
        cad3dNodePath: 'GANTRY-X-01',
        cad3dMeshId: 'mesh_GANTRY-X-01',
        localTransform: {
          x: 0,
          y: 1.35,
          z: 0,
          rotationX: 0,
          rotationY: 0,
          rotationZ: 0,
          scale: 1.0
        },
        boundingBox3d: {
          minX: -0.5,
          minY: 1.3,
          minZ: -0.2,
          maxX: 0.5,
          maxY: 1.5,
          maxZ: -0.1
        }
      }

      expect(cadRef.model3dUrl).toBe('/sample/station_digital_twin.glb')
      expect(cadRef.cad3dNodePath).toBe('GANTRY-X-01')
      expect(cadRef.localTransform?.y).toBe(1.35)
      expect(cadRef.boundingBox3d?.maxX).toBe(0.5)
    })
  })

  describe('3D CAD HUD Telemetry Overlay & Projected Node Coordinates', () => {
    it('calculates screen projection from 3D camera coordinates', () => {
      // Linear screen space projection formula verification:
      // screenX = (projected.x * 0.5 + 0.5) * width
      // screenY = (-projected.y * 0.5 + 0.5) * height
      const width = 800
      const height = 600

      // Center point (NDC: 0, 0, 0.5)
      const centerX = (0 * 0.5 + 0.5) * width
      const centerY = (-0 * 0.5 + 0.5) * height
      expect(centerX).toBe(400)
      expect(centerY).toBe(300)

      // Top-right point (NDC: 0.5, 0.5, 0.2)
      const trX = (0.5 * 0.5 + 0.5) * width
      const trY = (-0.5 * 0.5 + 0.5) * height
      expect(trX).toBe(600)
      expect(trY).toBe(150)
    })
  })
})
