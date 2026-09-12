import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { admin, username, organization, multiSession } from "better-auth/plugins";
import { adminAc, userAc } from "better-auth/plugins/admin/access";
import { useDb } from "./db"; // your drizzle instance
import * as hbSchema from "../database/drizzle/schema";
import { createDrizzleEventsProvider } from "./studioEventsProvider";

const socialProvidersConfig: Record<string, any> = {};

if (process.env.GITHUB_CLIENT_ID && process.env.GITHUB_CLIENT_SECRET) {
   socialProvidersConfig.github = {
      clientId: process.env.GITHUB_CLIENT_ID,
      clientSecret: process.env.GITHUB_CLIENT_SECRET
   };
}
if (process.env.GOOGLE_CLIENT_ID && process.env.GOOGLE_CLIENT_SECRET) {
   socialProvidersConfig.google = {
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET
   };
}
if (process.env.MICROSOFT_ENTRA_ID_CLIENT_ID && process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET) {
   socialProvidersConfig.microsoft = {
      clientId: process.env.MICROSOFT_ENTRA_ID_CLIENT_ID,
      clientSecret: process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET,
      tenantId: process.env.MICROSOFT_ENTRA_ID_TENANT_ID,
      scope: ["openid", "profile", "email", "User.Read", "Directory.Read.All"]
   };
}

const isProduction = process.env.NODE_ENV === 'production';
const rawAuthSecret = process.env.BETTER_AUTH_SECRET;

if (isProduction) {
   if (!rawAuthSecret || rawAuthSecret.trim() === '' || rawAuthSecret === "heimdall-default-dev-secret-key-32-chars-min-security" || rawAuthSecret === "heimdall-dev-secret-key-32-chars-min-security") {
      throw new Error("CRITICAL SECURITY CONFIGURATION ERROR: BETTER_AUTH_SECRET must be configured via environment variables in production and cannot match default development keys.");
   }
}

const authSecret = rawAuthSecret || "heimdall-default-dev-secret-key-32-chars-min-security";

export const auth = betterAuth({
   secret: authSecret,
   baseURL: process.env.BETTER_AUTH_URL || "http://localhost:3000",
   security: {
      allowedOrigins: [
         "http://localhost:3000",
         "http://127.0.0.1:3000",
         "http://localhost:5099",
         "http://127.0.0.1:5099"
      ]
   },
   database: drizzleAdapter(useDb(), {
      provider: "pg",
      schema: hbSchema
   }),
   databaseHooks: {
      session: {
         create: {
            after: async (session) => {
               try {
                  const provider = createDrizzleEventsProvider();
                  await provider.ingest({
                     id: crypto.randomUUID(),
                     type: "session.created",
                     timestamp: new Date(),
                     status: "success",
                     userId: session.userId,
                     sessionId: session.id,
                     source: "app",
                     display: {
                        message: `Session established for user (${session.userId})`,
                        severity: "info"
                     }
                  });
               } catch (e) {
                  console.error("[Auth Event Hook] Session create hook error:", e);
               }
            }
         }
      },
      user: {
         create: {
            after: async (user) => {
               try {
                  const provider = createDrizzleEventsProvider();
                  await provider.ingest({
                     id: crypto.randomUUID(),
                     type: "user.joined",
                     timestamp: new Date(),
                     status: "success",
                     userId: user.id,
                     metadata: { email: user.email, name: user.name },
                     source: "app",
                     display: {
                        message: `Identity enrolled: ${user.email}`,
                        severity: "success"
                     }
                  });
               } catch (e) {
                  console.error("[Auth Event Hook] User create hook error:", e);
               }
            }
         }
      }
   },
   user: {
      additionalFields: {
         role: { type: "string" }
      }
   },
   plugins: [
      admin({
         adminRoles: ["admin", "system_admin", "heimdall_admin", "engineering_admin"],
         roles: {
            admin: adminAc,
            system_admin: adminAc,
            heimdall_admin: adminAc,
            engineering_admin: adminAc,
            it_admin: userAc,
            manager: userAc,
            team_lead: userAc,
            engineer: userAc,
            technician: userAc,
            user: userAc
         }
      }),
      username(),
      organization(),
      multiSession()
   ],
   emailAndPassword: {
      enabled: true,
   },
   ...(Object.keys(socialProvidersConfig).length > 0 ? { socialProviders: socialProvidersConfig } : {})
});
