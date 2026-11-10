import { defineEventHandler, readBody, createError } from 'h3'
import { evaluateInfraAlert, clearDedupWindow, type InfraAlert } from '../../../../server/utils/infraAlertAutoTicketing'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || !body.alertType) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Alert type is required for infrastructure alert evaluation.'
    })
  }

  if (body.clearDedupWindow === true) {
    clearDedupWindow()
    return { success: true, message: 'Deduplication window cleared.' }
  }

  const alert: InfraAlert = {
    alertType: body.alertType,
    machineIdentifier: body.machineIdentifier || body.hostname || 'UNKNOWN',
    hostname: body.hostname || body.machineIdentifier || 'UNKNOWN',
    severity: body.severity || 'High',
    message: body.message || `Infrastructure alert: ${body.alertType}`,
    metricValue: body.metricValue,
    thresholdValue: body.thresholdValue,
    detectedAt: body.detectedAt || new Date().toISOString()
  }

  const result = evaluateInfraAlert(alert)

  return {
    success: true,
    alert: {
      type: alert.alertType,
      machine: alert.machineIdentifier,
      severity: alert.severity
    },
    ticket: result
  }
})
