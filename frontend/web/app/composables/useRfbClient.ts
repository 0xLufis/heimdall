/**
 * Composable / service wrapper for @novnc/novnc RFB client instantiation.
 * Isolates third-party DOM-dependent library imports for clean testing and SSR safety.
 */
export async function createRfbClient(
  target: HTMLElement,
  url: string,
  options: Record<string, any> = {}
) {
  if (typeof window === 'undefined') {
    throw new Error('RFB client cannot be created on server side.')
  }
  const { default: RFB } = await import('@novnc/novnc')
  return new RFB(target, url, options)
}
