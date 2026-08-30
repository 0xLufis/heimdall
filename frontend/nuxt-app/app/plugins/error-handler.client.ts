export default defineNuxtPlugin(() => {
  if (typeof window === 'undefined') return

  // Intercept and suppress unhandled third-party extension observer errors
  window.addEventListener('error', (event) => {
    const errorMsg = event.message || ''
    const filename = event.filename || ''

    // Suppress Web-Vitals / third-party performance observer extension errors
    if (
      (errorMsg.includes("reading 'startTime'") || errorMsg.includes('reportAllChanges')) &&
      (filename.includes('VM') || filename === '' || filename.includes('extension'))
    ) {
      event.preventDefault()
      event.stopPropagation()
      return true
    }
  }, true)

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason?.message || String(event.reason || '')
    if (reason.includes("reading 'startTime'") || reason.includes('reportAllChanges')) {
      event.preventDefault()
      event.stopPropagation()
    }
  })
})
