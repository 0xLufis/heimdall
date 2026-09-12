import { createAuthClient } from "better-auth/vue"
import { usernameClient, organizationClient, adminClient, multiSessionClient } from "better-auth/client/plugins"

/**
 * Initializes and exports the authentication client for the frontend.
 * This client is configured with Better-Auth plugins for username/password authentication,
 * admin capabilities, multi-session management, and organization support.
 * @type {ReturnType<typeof createAuthClient>}
 */
export const authClient = createAuthClient({
   plugins: [
      usernameClient(),
      organizationClient(),
      adminClient(),
      multiSessionClient()
   ]
})
