import { createAuthClient } from "better-auth/vue"
import { adminClient, usernameClient, organizationClient, multiSessionClient } from "better-auth/client/plugins"

export const authClient = createAuthClient({
    user: {
        additionalFields: {
            role: { type: "string" },
            username: { type: "string" }
        }
    },
    session: {
        additionalFields: {
            role: { type: "string" }
        }
    },
    plugins: [
        adminClient(),
        usernameClient(),
        organizationClient(),
        multiSessionClient()
    ]
})
