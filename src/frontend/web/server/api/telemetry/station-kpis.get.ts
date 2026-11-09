import { defineEventHandler, getQuery } from 'h3'

export interface StationTelemetryKpi {
  stationId: string
  handle: string
  name: string
  lineId: string
  lineName: string
  oee: number
  availability: number
  performance: number
  quality: number
  yield: number
  partsPerHour: number
  targetPartsPerHour: number
  cycleTimeSeconds: number
  targetCycleTimeSeconds: number
  totalPartsProduced: number
  scrapParts: number
  operationalStatus: 'RUNNING' | 'IDLE' | 'WARNING' | 'FAULTED' | 'MAINTENANCE'
  activeTicketCount: number
  criticalTicketCount: number
  hasSlaBreach: boolean
}

const LINE_BASELINES = [
  { lineId: 'L01', lineName: 'Line 1 - Pre-Assembly', availability: 97.2, performance: 94.5, quality: 99.4, oee: 91.3, stations: 12 },
  { lineId: 'L02', lineName: 'Line 2 - Screwing & Fastening', availability: 95.8, performance: 92.1, quality: 98.9, oee: 87.3, stations: 13 },
  { lineId: 'L03', lineName: 'Line 3 - Vision & Optical Quality', availability: 98.5, performance: 96.8, quality: 99.8, oee: 95.1, stations: 12 },
  { lineId: 'L04', lineName: 'Line 4 - Dispensing & Bonding', availability: 94.2, performance: 91.5, quality: 98.2, oee: 84.6, stations: 12 },
  { lineId: 'L05', lineName: 'Line 5 - Robotic Welding Cell', availability: 96.4, performance: 93.8, quality: 99.1, oee: 89.6, stations: 12 },
  { lineId: 'L06', lineName: 'Line 6 - Mechanical Subassembly', availability: 98.0, performance: 95.2, quality: 99.5, oee: 92.8, stations: 13 },
  { lineId: 'L07', lineName: 'Line 7 - High Voltage Battery Pack', availability: 95.1, performance: 93.0, quality: 99.6, oee: 88.1, stations: 13 },
  { lineId: 'L08', lineName: 'Line 8 - Powertrain Cell Integration', availability: 97.6, performance: 95.0, quality: 99.2, oee: 91.9, stations: 13 }
]

const OP_NAME_MAP: Record<number, string> = {
  10: 'Infeed Conveyor',
  20: 'CNC Machining Center',
  30: 'Dispensing & Bonding Cell',
  40: 'Robotic Transfer Cell',
  50: 'Automated Screwing Station',
  60: 'Laser Welding Cell',
  70: 'EOL Test Bench',
  80: 'Robot Deburring & Polishing',
  90: 'Vision Inspection Cell',
  100: 'Secondary Fastening Unit',
  110: 'Buffer Conveyor Bed',
  120: 'Outfeed Conveyor Bed',
  130: 'Packaging & Palletizing Cell'
}

export function generateStationKpis(filterLineId?: string, filterStationId?: string): StationTelemetryKpi[] {
  const result: StationTelemetryKpi[] = []

  for (const line of LINE_BASELINES) {
    if (filterLineId && line.lineId.toUpperCase() !== filterLineId.toUpperCase()) {
      continue
    }

    for (let op = 1; op <= line.stations; op++) {
      const opNum = op * 10
      const handle = `${line.lineId}-OP${String(opNum).padStart(3, '0')}`

      if (filterStationId && !handle.toUpperCase().includes(filterStationId.toUpperCase())) {
        continue
      }

      // Deterministic variance based on handle string hash
      let hash = 0
      for (let i = 0; i < handle.length; i++) {
        hash = (hash << 5) - hash + handle.charCodeAt(i)
        hash |= 0
      }
      hash = Math.abs(hash)

      const varA = ((hash % 11) - 5) * 0.4
      const varP = (((Math.floor(hash / 11)) % 11) - 5) * 0.6
      const varQ = (((Math.floor(hash / 121)) % 7) - 3) * 0.15

      const avail = Math.min(99.8, Math.max(76.0, Number((line.availability + varA).toFixed(1))))
      const perf = Math.min(99.6, Math.max(72.0, Number((line.performance + varP).toFixed(1))))
      const qual = Math.min(99.9, Math.max(95.0, Number((line.quality + varQ).toFixed(1))))
      const oee = Number(((avail * perf * qual) / 10000).toFixed(1))
      const yieldPct = Math.min(99.9, Math.max(94.0, Number((qual - 0.2 + (hash % 5) * 0.08).toFixed(1))))

      const partsPerHour = Math.round(142.0 * (perf / 94.0))
      const targetPartsPerHour = 150.0
      const cycleTimeSec = partsPerHour > 0 ? Number((3600.0 / partsPerHour).toFixed(1)) : 0.0
      const targetCycleTimeSec = 24.0

      const produced = 1000 + (hash % 500)
      const scrap = Math.max(0, Math.round(produced * (1.0 - yieldPct / 100.0)))

      // Specific known stations with simulated active SLA tickets
      let activeTickets = 0
      let criticalTickets = 0
      let hasSlaBreach = false

      if (['L01-OP030', 'L02-OP060', 'L04-OP040', 'L05-OP060', 'L07-OP020'].includes(handle)) {
        activeTickets = (hash % 3) + 1
        if (handle === 'L02-OP060' || handle === 'L04-OP040') {
          criticalTickets = 1
          hasSlaBreach = (handle === 'L02-OP060')
        }
      }

      let operationalStatus: StationTelemetryKpi['operationalStatus'] = 'RUNNING'
      if (hasSlaBreach || oee < 78.0) {
        operationalStatus = 'FAULTED'
      } else if (criticalTickets > 0 || oee < 85.0) {
        operationalStatus = 'WARNING'
      } else if (perf < 82.0) {
        operationalStatus = 'IDLE'
      }

      const typeName = OP_NAME_MAP[opNum] || 'Automation Cell'

      result.push({
        stationId: handle,
        handle,
        name: `${handle} ${typeName}`,
        lineId: line.lineId,
        lineName: line.lineName,
        oee,
        availability: avail,
        performance: perf,
        quality: qual,
        yield: yieldPct,
        partsPerHour,
        targetPartsPerHour,
        cycleTimeSeconds: cycleTimeSec,
        targetCycleTimeSeconds: targetCycleTimeSec,
        totalPartsProduced: produced,
        scrapParts: scrap,
        operationalStatus,
        activeTicketCount: activeTickets,
        criticalTicketCount: criticalTickets,
        hasSlaBreach
      })
    }
  }

  return result
}

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const lineId = query.lineId as string | undefined
  const stationId = query.stationId as string | undefined

  return generateStationKpis(lineId, stationId)
})
