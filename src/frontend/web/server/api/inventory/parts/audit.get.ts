import { defineEventHandler } from 'h3'

/** The backend keeps no inventory ledger yet; the drawer shows its empty state. */
export default defineEventHandler(() => ({ success: true, auditLog: [], available: false }))
