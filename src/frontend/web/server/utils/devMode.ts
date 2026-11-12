/**
 * Development switches of the BFF, in one place.
 *
 * `HEIMDALL_ENABLE_DEV=true` (set by `.env.dev`) is the only switch; a non-production `NODE_ENV`
 * alone never enables anything here. There is no separate demo flag: demo datasets run with the
 * same switch (see seed_data/demo/README.md).
 */

/** Explicit dev mode (`HEIMDALL_ENABLE_DEV=true`) or a Vitest run. */
export function isExplicitDevMode(): boolean {
  return process.env.HEIMDALL_ENABLE_DEV === 'true' || process.env.VITEST === 'true'
}

/**
 * Kiosk terminal routes (`/api/terminal/*`) skip authentication entirely. Only with an explicit
 * `HEIMDALL_ENABLE_DEV=true`, and never when `NODE_ENV=production`, whatever else is set.
 * Vitest does not enable it, so the tests exercise the real authentication paths.
 */
export function isTerminalAuthBypassEnabled(): boolean {
  if (process.env.NODE_ENV === 'production') return false
  return process.env.HEIMDALL_ENABLE_DEV === 'true'
}
