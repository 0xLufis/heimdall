import { defineEventHandler } from 'h3'

export default defineEventHandler(async () => {
  const workspaceId = 'a840e39b-7e61-4191-bb21-98782f93bc01'
  const reportId = '71c0490e-b812-4cf4-916b-70678d781bcf'
  const datasetId = '54b98df0-1011-477b-8911-39870198ad23'

  return {
    embedUrl: `https://app.powerbi.com/reportEmbed?reportId=${reportId}&groupId=${workspaceId}`,
    reportId,
    datasetId,
    workspaceId,
    isConfigured: false,
    authStatus: 'Demonstration Mock (Local Development)'
  }
})
