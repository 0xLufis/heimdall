import { ref, computed } from 'vue'

export interface MobileAlarmItem {
  id: string
  stationName: string
  code: string
  severity: 'Critical' | 'Warning' | 'Info'
  message: string
  activeDurationMinutes: number
  isLineStop: boolean
}

export interface MobileLineKpi {
  overallAvailabilityPercent: number
  shiftTargetParts: number
  currentPartsProduced: number
  activeLineStopsCount: number
  openTicketsCount: number
}

export function useMobileMetrics() {
  const kpis = ref<MobileLineKpi>({
    overallAvailabilityPercent: 94.2,
    shiftTargetParts: 5000,
    currentPartsProduced: 4890,
    activeLineStopsCount: 1,
    openTicketsCount: 4
  })

  const activeAlarms = ref<MobileAlarmItem[]>([
    {
      id: 'alm-1',
      stationName: 'OP20-Weld',
      code: 'ERR-SAFETY-01',
      severity: 'Critical',
      message: 'Safety Door Interlock Circuit Tripped',
      activeDurationMinutes: 8,
      isLineStop: true
    },
    {
      id: 'alm-2',
      stationName: 'OP30-Vision',
      code: 'ERR-QUAL-04',
      severity: 'Warning',
      message: 'Vision inspection camera lens contamination',
      activeDurationMinutes: 22,
      isLineStop: false
    }
  ])

  const productionProgressPercent = computed(() => {
    if (kpis.value.shiftTargetParts <= 0) return 0
    return Math.min(100, Math.round((kpis.value.currentPartsProduced / kpis.value.shiftTargetParts) * 100))
  })

  function acknowledgeAlarm(alarmId: string) {
    activeAlarms.value = activeAlarms.value.filter(a => a.id !== alarmId)
  }

  return {
    kpis,
    activeAlarms,
    productionProgressPercent,
    acknowledgeAlarm
  }
}
