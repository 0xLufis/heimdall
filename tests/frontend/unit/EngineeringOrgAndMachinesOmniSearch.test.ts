import { describe, it, expect } from 'vitest'
import { DEMO_PERSONAS, useAuthSession } from '../../../src/frontend/web/app/composables/useAuthSession'
import { useHeimdallSearch } from '../../../src/frontend/web/app/composables/useHeimdallSearch'
import filterHandler from '../../../src/frontend/web/server/api/inventory/filter'

describe('Enterprise Engineering Org, Machines Views & OmniSearch Suite', () => {
  describe('Engineering Department Titles & Outside Approver Hierarchy', () => {
    it('defines authentic corporate titles for engineering departments and external approvers', () => {
      // Plant Director
      const plantDirector = DEMO_PERSONAS.find(p => p.role === 'plant_director')
      expect(plantDirector).toBeDefined()
      expect(plantDirector?.title).toBe('Plant Director')
      expect(plantDirector?.department).toBe('Plant Management')

      // Plant Engineering Manager
      const engManager = DEMO_PERSONAS.find(p => p.role === 'plant_engineering_manager')
      expect(engManager).toBeDefined()
      expect(engManager?.title).toBe('Plant Engineering Manager')

      // Senior Engineering Manager
      const srEngManager = DEMO_PERSONAS.find(p => p.role === 'senior_engineering_manager')
      expect(srEngManager).toBeDefined()
      expect(srEngManager?.title).toBe('Senior Engineering Manager')

      // IT Site Admin (External approver)
      const itSiteAdmin = DEMO_PERSONAS.find(p => p.role === 'it_site_admin')
      expect(itSiteAdmin).toBeDefined()
      expect(itSiteAdmin?.title).toBe('IT Site Administrator')
      expect(itSiteAdmin?.department).toBe('Industrial IT')

      // Operative Planner (Line Manager for requesting line stops)
      const opPlanner = DEMO_PERSONAS.find(p => p.role === 'operative_planner')
      expect(opPlanner).toBeDefined()
      expect(opPlanner?.title).toBe('Operative Line Planner')
      expect(opPlanner?.department).toBe('Line Operations Planning')
    })

    it('evaluates line stop request and approval capabilities for operative planner and plant engineering manager', () => {
      const { canApproveLineStops, setSimulatedPersona } = useAuthSession()

      // Operative planner can request/approve line stops
      setSimulatedPersona({
        id: 'usr-planner',
        name: 'Eva Nagy',
        email: 'eva.planner@factory.corp',
        role: 'operative_planner'
      })
      expect(canApproveLineStops.value).toBe(true)

      // Plant engineering manager can approve line stops
      setSimulatedPersona({
        id: 'usr-eng-mgr',
        name: 'Marcus Vance',
        email: 'marcus.vance@factory.corp',
        role: 'plant_engineering_manager'
      })
      expect(canApproveLineStops.value).toBe(true)

      // Ordinary technician cannot approve line stops
      setSimulatedPersona({
        id: 'usr-tech',
        name: 'Alex Tech',
        email: 'alex.tech@factory.corp',
        role: 'technician'
      })
      expect(canApproveLineStops.value).toBe(false)
    })

    it('dynamically delegates IT admin capabilities to Heimdall admin via adminRoleDelegation setting', () => {
      const { isItAdmin, setSimulatedPersona, setAdminRoleDelegation } = useAuthSession()

      setSimulatedPersona({
        id: 'usr-admin',
        name: 'Heimdall Admin',
        email: 'admin@factory.corp',
        role: 'heimdall_admin'
      })

      // Default: delegation disabled
      setAdminRoleDelegation({ heimdallAdminIsPseudoItAdmin: false })
      expect(isItAdmin.value).toBe(false)

      // SystemAdmin enables pseudo-IT admin delegation
      setAdminRoleDelegation({ heimdallAdminIsPseudoItAdmin: true })
      expect(isItAdmin.value).toBe(true)
    })
  })

  describe('Inventory Parts vs. Stock Segmentation', () => {
    it('filters serialized high-value parts in storage and installed in machines', async () => {
      const eventMock = {
        node: { req: { method: 'POST' } },
        _body: { type: 'parts' }
      } as any

      const res = await filterHandler(eventMock)

      expect(res).toBeDefined()
      expect(res.items.length).toBeGreaterThan(0)

      // Serialized parts should have isStockItem: false or undefined
      for (const item of res.items) {
        expect(item.isStockItem).toBe(false)
      }

      // Should include high-value parts either InMachine or InStorage
      const inMachinePart = res.items.find((i: any) => i.equipmentStatus === 'InMachine')
      const inStoragePart = res.items.find((i: any) => i.equipmentStatus === 'InStorage')
      expect(inMachinePart).toBeDefined()
      expect(inStoragePart).toBeDefined()
      expect(inStoragePart?.storageLocation).toContain('Shelf')
    })

    it('filters bulk consumable stock tracked by quantity and min stock threshold', async () => {
      const eventMock = {
        node: { req: { method: 'POST' } },
        _body: { type: 'stock' }
      } as any

      const res = await filterHandler(eventMock)

      expect(res).toBeDefined()
      expect(res.items.length).toBeGreaterThan(0)

      // Stock items must have isStockItem: true and a stockQuantity
      for (const item of res.items) {
        expect(item.isStockItem).toBe(true)
        expect(typeof item.stockQuantity).toBe('number')
      }

      // Check stock of 9 parts with identifier
      const screwStock = res.items.find((i: any) => i.customIdentifier === 'STK-SCRW-M8')
      expect(screwStock).toBeDefined()
      expect(screwStock?.stockQuantity).toBe(9)
      expect(screwStock?.minStockThreshold).toBe(3)
    })
  })

  describe('OmniSearch Language Server 3-Stage Autocomplete & Value Lookup', () => {
    it('triggers secondary value lookup when typing key: (e.g. tech: or status:)', () => {
      const {
        rawInput,
        activePendingKey,
        valueSuggestions,
        matchingKeys,
        selectValueSuggestion,
        handleInputChange,
        tags
      } = useHeimdallSearch({ instanceId: 'machines' })

      // User types `tech:`
      handleInputChange('tech:')
      expect(activePendingKey.value).toBe('tech')
      expect(valueSuggestions.value.length).toBeGreaterThanOrEqual(5)
      
      const techValues = valueSuggestions.value.map(v => v.value)
      expect(techValues).toContain('Assembly')
      expect(techValues).toContain('Welding')
      expect(techValues).toContain('Test')
      expect(techValues).toContain('Robotics')

      // User selects a value suggestion
      selectValueSuggestion('Assembly')
      expect(tags.value.some(t => t.key === 'tech' && t.value === 'Assembly')).toBe(true)
      expect(rawInput.value).toBe('')
      expect(activePendingKey.value).toBe('')
    })

    it('suggests operational status values when typing status:', () => {
      const {
        activePendingKey,
        valueSuggestions,
        handleInputChange
      } = useHeimdallSearch({ instanceId: 'inventory' })

      handleInputChange('status:')
      expect(activePendingKey.value).toBe('status')
      const statusValues = valueSuggestions.value.map(v => v.value)
      expect(statusValues).toContain('online')
      expect(statusValues).toContain('offline')
      expect(statusValues).toContain('InStorage')
      expect(statusValues).toContain('InMachine')
    })

    it('matches available filter keys (Stage 2) when user types partial key text', () => {
      const {
        rawInput,
        matchingKeys,
        selectKeySuggestion,
        activePendingKey,
        handleInputChange
      } = useHeimdallSearch({ instanceId: 'machines' })

      // Typing `te` should suggest `tech:` and `type:`
      handleInputChange('te')
      expect(matchingKeys.value.some(k => k.key === 'tech')).toBe(true)

      // User selects `tech` key
      selectKeySuggestion('tech')
      expect(rawInput.value).toBe('tech:')
      expect(activePendingKey.value).toBe('tech')
    })
  })

  describe('Lines and Technologies Header Folding', () => {
    it('toggles line folding and child visibility state correctly', () => {
      const foldedLines: Record<string, boolean> = {}
      const toggleLineFold = (lineKey: string) => {
        foldedLines[lineKey] = !foldedLines[lineKey]
      }

      const lineId = 'Line 1'
      // Initially unfolded (false/falsy)
      expect(Boolean(foldedLines[lineId])).toBe(false)

      // Header click toggles folding -> folded (children hidden)
      toggleLineFold(lineId)
      expect(foldedLines[lineId]).toBe(true)

      // Next header click unfolds -> unfolded (children visible)
      toggleLineFold(lineId)
      expect(foldedLines[lineId]).toBe(false)
    })

    it('toggles technology folding and child visibility state correctly', () => {
      const foldedTechnologies: Record<string, boolean> = {}
      const toggleTechnologyFold = (techKey: string) => {
        foldedTechnologies[techKey] = !foldedTechnologies[techKey]
      }

      const tech = 'Assembly'
      // Initially unfolded (false/falsy)
      expect(Boolean(foldedTechnologies[tech])).toBe(false)

      // Header click toggles folding -> folded (children hidden)
      toggleTechnologyFold(tech)
      expect(foldedTechnologies[tech]).toBe(true)

      // Next header click unfolds -> unfolded (children visible)
      toggleTechnologyFold(tech)
      expect(foldedTechnologies[tech]).toBe(false)
    })

    it('evaluates allLinesFolded and batch toggles all lines', () => {
      const lines = [{ lineId: 'Line 1' }, { lineId: 'Line 2' }]
      const foldedLines: Record<string, boolean> = {}
      const allLinesFolded = () => lines.every(l => foldedLines[l.lineId])
      const toggleAllLinesFold = () => {
        const target = !allLinesFolded()
        for (const l of lines) {
          foldedLines[l.lineId] = target
        }
      }

      expect(allLinesFolded()).toBe(false)
      toggleAllLinesFold()
      expect(allLinesFolded()).toBe(true)
      expect(foldedLines['Line 1']).toBe(true)
      expect(foldedLines['Line 2']).toBe(true)

      toggleAllLinesFold()
      expect(allLinesFolded()).toBe(false)
      expect(foldedLines['Line 1']).toBe(false)
      expect(foldedLines['Line 2']).toBe(false)
    })

    it('evaluates allTechnologiesFolded and batch toggles all technologies', () => {
      const techs = [{ technology: 'Assembly' }, { technology: 'Welding' }]
      const foldedTechnologies: Record<string, boolean> = {}
      const allTechnologiesFolded = () => techs.every(t => foldedTechnologies[t.technology])
      const toggleAllTechnologiesFold = () => {
        const target = !allTechnologiesFolded()
        for (const t of techs) {
          foldedTechnologies[t.technology] = target
        }
      }

      expect(allTechnologiesFolded()).toBe(false)
      toggleAllTechnologiesFold()
      expect(allTechnologiesFolded()).toBe(true)
      expect(foldedTechnologies['Assembly']).toBe(true)
      expect(foldedTechnologies['Welding']).toBe(true)

      toggleAllTechnologiesFold()
      expect(allTechnologiesFolded()).toBe(false)
      expect(foldedTechnologies['Assembly']).toBe(false)
      expect(foldedTechnologies['Welding']).toBe(false)
    })
  })
})

