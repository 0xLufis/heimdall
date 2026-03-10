import { authClient } from "~/utils/auth-client"

export default defineNuxtRouteMiddleware(async (to, from) => {
    // Avoid running on certain paths
    if (to.path.startsWith('/api/') || to.path.includes('favicon.ico') || to.path.startsWith('/_nuxt')) {
        return
    }

    try {
        let session = null
        
        if (import.meta.server) {
            const headers = useRequestHeaders(['cookie'])
            const response = await authClient.getSession({
                fetchOptions: {
                    headers: headers as any
                }
            })
            session = response.data
        } else {
            const response = await authClient.getSession()
            session = response.data
        }
        
        if (!session) {
            if (to.path.startsWith('/dashboard')) {
                return navigateTo('/auth/login')
            }
        } else {
            if (to.path.startsWith('/auth')) {
                return navigateTo('/dashboard')
            }
        }
    } catch (e: any) {
        // Log the actual error to the server console before it hits the error page
        console.error("--- AUTH MIDDLEWARE CRASH ---")
        console.error("Path:", to.path)
        console.error("Error Message:", e?.message || "No message")
        if (e?.stack) {
            console.error("Stack Trace:", e.stack)
        }
        console.error("-----------------------------")
        
        // Fail-safe: if we're trying to reach dashboard, redirect to login
        if (to.path.startsWith('/dashboard')) {
            return navigateTo('/auth/login')
        }
    }
})
