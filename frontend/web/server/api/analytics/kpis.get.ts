import { defineEventHandler } from 'h3'

export default defineEventHandler(async () => {
  return [
    { lineId: 'L01', lineName: 'Line 1 - Pre-Assembly', availability: 97.2, performance: 94.5, quality: 99.4, oee: 91.3, mtbfHours: 380, mttrMinutes: 28, incidentCount: 12 },
    { lineId: 'L02', lineName: 'Line 2 - Screwing & Fastening', availability: 95.8, performance: 92.1, quality: 98.9, oee: 87.3, mtbfHours: 310, mttrMinutes: 35, incidentCount: 18 },
    { lineId: 'L03', lineName: 'Line 3 - Vision & Optical Quality', availability: 98.5, performance: 96.8, quality: 99.8, oee: 95.1, mtbfHours: 520, mttrMinutes: 22, incidentCount: 6 },
    { lineId: 'L04', lineName: 'Line 4 - Dispensing & Bonding', availability: 94.2, performance: 91.5, quality: 98.2, oee: 84.6, mtbfHours: 265, mttrMinutes: 42, incidentCount: 21 },
    { lineId: 'L05', lineName: 'Line 5 - Robotic Welding Cell', availability: 96.4, performance: 93.8, quality: 99.1, oee: 89.6, mtbfHours: 340, mttrMinutes: 32, incidentCount: 14 },
    { lineId: 'L06', lineName: 'Line 6 - Mechanical Subassembly', availability: 98.0, performance: 95.2, quality: 99.5, oee: 92.8, mtbfHours: 440, mttrMinutes: 26, incidentCount: 9 },
    { lineId: 'L07', lineName: 'Line 7 - High Voltage Battery Pack', availability: 95.1, performance: 93.0, quality: 99.6, oee: 88.1, mtbfHours: 295, mttrMinutes: 38, incidentCount: 16 },
    { lineId: 'L08', lineName: 'Line 8 - Powertrain Cell Integration', availability: 97.6, performance: 95.0, quality: 99.2, oee: 91.9, mtbfHours: 410, mttrMinutes: 30, incidentCount: 10 }
  ]
})
