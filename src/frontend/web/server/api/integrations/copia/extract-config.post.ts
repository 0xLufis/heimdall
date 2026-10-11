import { defineEventHandler, readBody, createError } from 'h3'
import { extractPlcConfig, TWINCAT_GITIGNORE_PATTERNS } from '../../../../server/utils/plcConfigExtractor'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}
  const hostname = body.hostname || body.controllerHostname || 'IPC-STATION-01'

  // Sample files if none provided in request
  const rawFiles = body.files || [
    {
      path: 'POUs/MAIN.TcPOU',
      content: 'PROGRAM MAIN\nVAR\n  bCycleRunning : BOOL;\n  nPartCounter : UDINT;\nEND_VAR\n\nIF bCycleRunning THEN\n  nPartCounter := nPartCounter + 1;\nEND_IF;'
    },
    {
      path: 'GVLs/GVL_MachineState.TcGVL',
      content: 'VAR_GLOBAL\n  sActiveRecipe : STRING := \'WeldProfile_A\';\n  rSpindleTemp : REAL := 42.5;\nEND_VAR'
    },
    {
      path: 'DUTs/ST_PartTelemetry.TcDUT',
      content: 'TYPE ST_PartTelemetry :\nSTRUCT\n  nSerialId : ULINT;\n  bPassedVision : BOOL;\n  rCycleDurationMs : LREAL;\nEND_STRUCT\nEND_TYPE'
    },
    {
      path: 'TwinCAT Project.tsproj',
      content: '<TcSmProject ProjectVersion="3.1.4024" />'
    },
    // Transient build and compile artifacts that must be filtered out
    {
      path: '_Boot/TwinCAT/Plc/TcPlcApp.tszip',
      content: 'TRANSIENT_BOOT_BINARY_DATA'
    },
    {
      path: '_CompileInfo/PlcApp.compileinfo',
      content: 'TRANSIENT_COMPILER_INFO'
    },
    {
      path: 'bin/TcPlcApp.tpy',
      content: 'TRANSIENT_TPY_FILE'
    }
  ]

  const result = extractPlcConfig(hostname, rawFiles, {
    projectName: body.projectName
  })

  return {
    success: true,
    extraction: result,
    gitignorePatterns: TWINCAT_GITIGNORE_PATTERNS
  }
})
