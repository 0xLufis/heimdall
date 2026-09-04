import { defineEventHandler, readBody, getMethod, getQuery } from 'h3'

export default defineEventHandler(async (event) => {
  const method = getMethod(event)
  const action = event.context.params?.action || 'status'
  
  // Connect to simulator container on docker network or localhost
  const simHost = process.env.SIMULATOR_HOST || 'http://simulator:5055'

  try {
    const url = `${simHost}/api/${action}`
    const options: any = {
      method,
      headers: { 'Content-Type': 'application/json' },
      query: getQuery(event)
    }

    if (method === 'POST' || method === 'PUT') {
      options.body = await readBody(event).catch(() => ({}))
    }

    const res = await $fetch(url, options)
    return res
  } catch (err: any) {
    // Return fallback status if simulator container is restarting or offline
    return {
      active_fleet: 50,
      total_dispatched: 0,
      total_errors: 0,
      fault_rate: 0.02,
      nodes: [],
      sim_offline: true,
      error: err.message
    }
  }
})
