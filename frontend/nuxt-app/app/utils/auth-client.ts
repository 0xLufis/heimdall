import { createAuthClient } from "better-auth/vue"
import { usernameClient, organizationClient } from "better-auth/client/plugins"

export const authClient = createAuthClient({
   plugins: [
      usernameClient(),
      organizationClient()
   ]
})
