import { isTerminalAuthBypassEnabled } from '../utils/devMode'

/** Says once, at startup, that the kiosk terminal routes run without authentication. */
export default defineNitroPlugin(() => {
  if (isTerminalAuthBypassEnabled()) {
    console.warn(
      '[terminal-auth] HEIMDALL_ENABLE_DEV=true: /api/terminal/* accepts unauthenticated requests (dev/demo kiosk bypass). ' +
      'NODE_ENV=production always disables it.'
    )
  }
})
