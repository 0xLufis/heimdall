import { createAuthClient } from "better-auth/vue"
import { usernameClient, organizationClient } from "better-auth/client/plugins"

/**
 * Initializes and exports the authentication client for the frontend.
 * This client is configured with Better-Auth plugins for username/password authentication
 * and organization support, providing a centralized interface for authentication operations
 * across the application.
 * @type {ReturnType<typeof createAuthClient>}
 */
export const authClient = createAuthClient({
   plugins: [
      usernameClient(),
      organizationClient()
   ]
})
