import { describe, it, expect, vi } from 'vitest'
import {
  reorderAndPrioritize,
  setDragImageAtClickPoint,
  setLastPointerPositionForTesting,
  type PrioritizedItem
} from '~/utils/reorderList'

interface MockRule extends PrioritizedItem {
  id: string
  name: string
  priority: number
  enabled: boolean
}

describe('Rule Reordering & Drag-and-Drop Sequential Priority Suite', () => {
  const initialRules: MockRule[] = [
    { id: 'rule-1', name: 'Beckhoff Motion', priority: 1, enabled: true },
    { id: 'rule-2', name: 'Cognex Vision', priority: 2, enabled: true },
    { id: 'rule-3', name: 'Standard Baseline', priority: 3, enabled: false },
    { id: 'rule-4', name: 'Safety Fastening', priority: 4, enabled: true }
  ]

  it('moves a rule downward in priority (index 0 to index 2) and updates priorities 1..N', () => {
    const reordered = reorderAndPrioritize(initialRules, 0, 2)

    expect(reordered.map(r => r.id)).toEqual(['rule-2', 'rule-3', 'rule-1', 'rule-4'])
    expect(reordered.map(r => r.priority)).toEqual([1, 2, 3, 4])
    expect(reordered[2].name).toBe('Beckhoff Motion')
    expect(reordered[2].priority).toBe(3)
  })

  it('moves a rule upward in priority (index 3 to index 0) and updates priorities 1..N', () => {
    const reordered = reorderAndPrioritize(initialRules, 3, 0)

    expect(reordered.map(r => r.id)).toEqual(['rule-4', 'rule-1', 'rule-2', 'rule-3'])
    expect(reordered.map(r => r.priority)).toEqual([1, 2, 3, 4])
    expect(reordered[0].id).toBe('rule-4')
    expect(reordered[0].priority).toBe(1)
  })

  it('swaps adjacent items cleanly (index 1 to index 2)', () => {
    const reordered = reorderAndPrioritize(initialRules, 1, 2)

    expect(reordered.map(r => r.id)).toEqual(['rule-1', 'rule-3', 'rule-2', 'rule-4'])
    expect(reordered.map(r => r.priority)).toEqual([1, 2, 3, 4])
  })

  it('returns an identical shallow copy when fromIndex equals toIndex', () => {
    const reordered = reorderAndPrioritize(initialRules, 1, 1)

    expect(reordered).toEqual(initialRules)
    expect(reordered).not.toBe(initialRules) // Should be a new array copy
  })

  it('handles negative or out-of-bounds indices safely without throwing', () => {
    expect(reorderAndPrioritize(initialRules, -1, 2)).toEqual(initialRules)
    expect(reorderAndPrioritize(initialRules, 1, 10)).toEqual(initialRules)
    expect(reorderAndPrioritize(initialRules, 99, 1)).toEqual(initialRules)
    expect(reorderAndPrioritize([], 0, 1)).toEqual([])
  })

  it('preserves all custom metadata fields and flags (enabled, tags, etc.)', () => {
    const rulesWithTags = [
      { id: 'r1', name: 'Rule 1', priority: 1, enabled: true, tags: ['Beckhoff'], ouPatterns: ['*OU1*'] },
      { id: 'r2', name: 'Rule 2', priority: 2, enabled: false, tags: ['Cognex'], ouPatterns: [] }
    ]

    const result = reorderAndPrioritize(rulesWithTags, 1, 0)

    expect(result[0].id).toBe('r2')
    expect(result[0].priority).toBe(1)
    expect(result[0].enabled).toBe(false)
    expect(result[0].tags).toEqual(['Cognex'])

    expect(result[1].id).toBe('r1')
    expect(result[1].priority).toBe(2)
    expect(result[1].tags).toEqual(['Beckhoff'])
    expect(result[1].ouPatterns).toEqual(['*OU1*'])
  })

  describe('ConfigurePage DOM Drag & Drop Integration', () => {
    it('renders rule cards with draggable="true" and GripVertical drag handles', async () => {
      const { mount } = await import('@vue/test-utils')
      const { ref } = await import('vue')
      const { default: ConfigurePage } = await import('~/pages/dashboard/telemetry/configure.vue')

      // Mock globals
      const nuxtStates = new Map<string, any>()
      vi.stubGlobal('definePageMeta', vi.fn())
      vi.stubGlobal('useState', (key: string, init?: () => any) => {
        if (!nuxtStates.has(key)) {
          nuxtStates.set(key, ref(init ? init() : undefined))
        }
        return nuxtStates.get(key)
      })
      vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
        if (url === '/api/telemetry/policy') {
          return Promise.resolve({
            assignedTemplateId: 'standard-factory-baseline',
            ouTagRules: initialRules
          })
        }
        return Promise.resolve([])
      }))

      const wrapper = mount(ConfigurePage, {
        global: {
          stubs: {
            // Stub heavy subcomponents if needed
            DashboardInventoryTable: true,
            Table: true,
            TableHeader: true,
            TableBody: true,
            TableRow: true,
            TableHead: true,
            TableCell: true
          }
        }
      })

      // Rule cards must be draggable
      const draggableCards = wrapper.findAll('[draggable="true"]')
      expect(draggableCards.length).toBeGreaterThan(0)

      // Grip handles with title must be present
      const gripHandles = wrapper.findAll('[title="Drag and drop to reorder evaluation priority"]')
      expect(gripHandles.length).toBe(draggableCards.length)
    })
  })

  describe('setDragImageAtClickPoint Point-of-Click Anchoring Suite', () => {
    it('sets drag image offset to exact click coordinates relative to the card using a clone', () => {
      const setDragImageMock = vi.fn()
      const mockCard = document.createElement('div')
      mockCard.setAttribute('draggable', 'true')
      mockCard.getBoundingClientRect = vi.fn().mockReturnValue({
        left: 100,
        top: 200,
        width: 400,
        height: 100,
        right: 500,
        bottom: 300
      })

      const mockEvent = {
        clientX: 250,
        clientY: 240,
        currentTarget: mockCard,
        target: mockCard,
        dataTransfer: {
          setDragImage: setDragImageMock
        }
      } as unknown as DragEvent

      setDragImageAtClickPoint(mockEvent)

      expect(setDragImageMock).toHaveBeenCalledTimes(1)
      // offsetX = 250 - 100 = 150; offsetY = 240 - 200 = 40
      expect(setDragImageMock).toHaveBeenCalledWith(expect.any(HTMLElement), 150, 40)
      const passedClone = setDragImageMock.mock.calls[0][0] as HTMLElement
      expect(passedClone.style.width).toBe('400px')
      expect(passedClone.style.height).toBe('100px')
    })

    it('resolves parent draggable card when user clicks an inner child element', () => {
      const setDragImageMock = vi.fn()
      const parentCard = document.createElement('div')
      parentCard.setAttribute('draggable', 'true')
      parentCard.getBoundingClientRect = vi.fn().mockReturnValue({
        left: 50,
        top: 80,
        width: 300,
        height: 60
      })

      const innerGripHandle = document.createElement('div')
      parentCard.appendChild(innerGripHandle)

      const mockEvent = {
        clientX: 65,
        clientY: 95,
        currentTarget: parentCard,
        target: innerGripHandle,
        dataTransfer: {
          setDragImage: setDragImageMock
        }
      } as unknown as DragEvent

      setDragImageAtClickPoint(mockEvent)

      expect(setDragImageMock).toHaveBeenCalledTimes(1)
      // offsetX = 65 - 50 = 15; offsetY = 95 - 80 = 15
      expect(setDragImageMock).toHaveBeenCalledWith(expect.any(HTMLElement), 15, 15)
    })

    it('falls back to captured pointerdown coordinates when DragEvent clientX is 0 (Linux bug)', () => {
      const setDragImageMock = vi.fn()
      const mockCard = document.createElement('div')
      mockCard.setAttribute('draggable', 'true')
      mockCard.getBoundingClientRect = vi.fn().mockReturnValue({
        left: 100,
        top: 200,
        width: 400,
        height: 100
      })

      // Simulate Linux Chromium event reporting clientX: 0
      const mockEvent = {
        clientX: 0,
        clientY: 0,
        currentTarget: mockCard,
        target: mockCard,
        dataTransfer: {
          setDragImage: setDragImageMock
        }
      } as unknown as DragEvent

      // Set pointerdown coordinate fallback
      setLastPointerPositionForTesting({ x: 300, y: 250 })

      setDragImageAtClickPoint(mockEvent)

      expect(setDragImageMock).toHaveBeenCalledTimes(1)
      // offsetX = 300 - 100 = 200; offsetY = 250 - 200 = 50
      expect(setDragImageMock).toHaveBeenCalledWith(expect.any(HTMLElement), 200, 50)

      // Clean up testing state
      setLastPointerPositionForTesting(null)
    })

    it('gracefully degrades when dataTransfer or setDragImage is not supported', () => {
      const mockCard = document.createElement('div')
      const mockEventNoDt = {
        clientX: 10,
        clientY: 10,
        target: mockCard
      } as unknown as DragEvent

      expect(() => setDragImageAtClickPoint(mockEventNoDt)).not.toThrow()
    })
  })
})

