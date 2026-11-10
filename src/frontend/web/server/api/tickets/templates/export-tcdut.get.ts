import { defineEventHandler, setHeader } from 'h3'
import { exportTwinCatDut, getTemplatesStore } from '../../../utils/ticketTemplatesStore'

export default defineEventHandler((event) => {
  const templates = getTemplatesStore()
  const dutXml = exportTwinCatDut(templates)

  setHeader(event, 'Content-Type', 'application/xml; charset=utf-8')
  setHeader(event, 'Content-Disposition', 'attachment; filename="E_HeimdallErrorCode.TcDUT"')

  return dutXml
})
