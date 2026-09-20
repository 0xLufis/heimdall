import { defineEventHandler } from 'h3'
import { featureFlags } from '../../utils/featureFlags'

const DEV_WORKSPACE_ID = 'a840e39b-7e61-4191-bb21-98782f93bc01'
const DEV_REPORT_ID = '71c0490e-b812-4cf4-916b-70678d781bcf'
const DEV_DATASET_ID = '54b98df0-1011-477b-8911-39870198ad23'

export default defineEventHandler(async () => {
  const workspaceId = process.env.POWERBI_WORKSPACE_ID || (featureFlags.enableDevFeatures ? DEV_WORKSPACE_ID : '')
  const reportId = process.env.POWERBI_REPORT_ID || (featureFlags.enableDevFeatures ? DEV_REPORT_ID : '')
  const datasetId = process.env.POWERBI_DATASET_ID || (featureFlags.enableDevFeatures ? DEV_DATASET_ID : '')

  const isConfigured = Boolean(process.env.POWERBI_WORKSPACE_ID && process.env.POWERBI_REPORT_ID)

  return {
    embedUrl: reportId && workspaceId ? `https://app.powerbi.com/reportEmbed?reportId=${reportId}&groupId=${workspaceId}` : '',
    reportId,
    datasetId,
    workspaceId,
    isConfigured,
    authStatus: isConfigured ? 'Connected' : 'Demonstration Mock (Local Development)'
  }
})
