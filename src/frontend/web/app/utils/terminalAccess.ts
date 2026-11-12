/**
 * How the kiosk station terminal (`pages/terminal/machine/[id].vue`) explains a failed load and
 * how the workstation was authenticated (`workstation.authenticatedVia` of
 * `GET /api/terminal/machine/:id`).
 */

export interface TerminalLoadFailure {
  /** Text shown on the pairing screen. */
  message: string
  /** 403 from the computer-certificate path: authenticated, but bound to other stations. */
  notAssigned: boolean
}

/** The failure text for a rejected terminal load; `stationLabel` names the requested station. */
export function describeTerminalLoadError(err: unknown, stationLabel: string): TerminalLoadFailure {
  const e = (err ?? {}) as { statusCode?: number, status?: number, statusMessage?: string, data?: { statusMessage?: string } }
  const status = e.statusCode ?? e.status
  const station = stationLabel.trim() || 'this station'
  if (status === 403) {
    return { message: `This workstation is not assigned to station ${station}.`, notAssigned: true }
  }
  if (status === 401) {
    return { message: 'Station token invalid or unauthenticated.', notAssigned: false }
  }
  return { message: e.statusMessage || e.data?.statusMessage || 'Unable to load machine terminal data.', notAssigned: false }
}

/** True when the BFF let the terminal in without any authentication (development bypass). */
export function isDevBypass(response: unknown): boolean {
  const workstation = (response as { workstation?: { authenticatedVia?: unknown } } | null)?.workstation
  return workstation?.authenticatedVia === 'DevBypass'
}
