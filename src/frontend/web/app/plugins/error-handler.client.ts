export default defineNuxtPlugin(() => {
  if (typeof window === 'undefined') return

  const isThirdPartyNoise = (msg: string, stack?: string, file?: string) => {
    const text = `${msg} ${stack || ''} ${file || ''}`
    return text.includes("reading 'startTime'") || text.includes('reportAllChanges')
  }

  // Intercept and suppress unhandled third-party extension observer errors
  window.addEventListener('error', (event) => {
    const errorMsg = event.message || ''
    const filename = event.filename || ''
    const stack = event.error?.stack || ''

    if (isThirdPartyNoise(errorMsg, stack, filename)) {
      event.preventDefault()
      event.stopPropagation()
      return true
    }
  }, true)

  const origOnError = window.onerror
  window.onerror = (message, source, lineno, colno, error) => {
    const msg = String(message || '')
    const src = String(source || '')
    const stack = error?.stack || ''
    if (isThirdPartyNoise(msg, stack, src)) {
      return true
    }
    if (origOnError) {
      return origOnError(message, source, lineno, colno, error)
    }
    return false
  }

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason?.message || String(event.reason || '')
    const stack = event.reason?.stack || ''
    if (isThirdPartyNoise(reason, stack)) {
      event.preventDefault()
      event.stopPropagation()
    }
  })

  const origConsoleError = console.error
  console.error = (...args: any[]) => {
    const combined = args.map(a => (typeof a === 'object' && a?.stack ? a.stack : String(a))).join(' ')
    if (isThirdPartyNoise(combined)) {
      return
    }
    origConsoleError.apply(console, args)
  }
})
