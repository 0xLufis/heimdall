import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { admin, username, organization, multiSession } from "better-auth/plugins";
import { adminAc, userAc } from "better-auth/plugins/admin/access";
import { eq } from "drizzle-orm";
import { useDb } from "./db"; // your drizzle instance
import * as hbSchema from "../database/drizzle/schema";
import { createDrizzleEventsProvider } from "./studioEventsProvider";
import { getPlantUsers } from "./datasetLoader";
import { syncUserSecurityGroupsToOrganizations } from "./securityGroupOrgSync";

export function getAzureSsoConfig() {
   const clientId = process.env.AZURE_AD_CLIENT_ID 
      || process.env.MICROSOFT_ENTRA_ID_CLIENT_ID 
      || process.env.AZURE_CLIENT_ID;
   const clientSecret = process.env.AZURE_AD_CLIENT_SECRET 
      || process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET 
      || process.env.AZURE_CLIENT_SECRET;
   const tenantId = process.env.AZURE_AD_TENANT_ID 
      || process.env.MICROSOFT_ENTRA_ID_TENANT_ID 
      || process.env.AZURE_TENANT_ID 
      || "72f988bf-86f1-41af-91ab-2d7cd011db47"; // canonical enterprise plant tenant

   const isConfigured = !!(clientId && clientSecret);

   return {
      clientId,
      clientSecret,
      tenantId,
      isConfigured
   };
}

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

const azureConfig = getAzureSsoConfig();
if (azureConfig.isConfigured) {
   socialProvidersConfig.microsoft = {
      clientId: azureConfig.clientId!,
      clientSecret: azureConfig.clientSecret!,
      tenantId: azureConfig.tenantId,
      scope: ["openid", "profile", "email", "User.Read", "offline_access"]
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
                  const db = useDb();
                  const plantUsers = getPlantUsers();
                  const matchedDirUser = plantUsers.find(u => u.email.toLowerCase() === (user.email || '').toLowerCase());
                  const assignedRole = (user as any).role || matchedDirUser?.primaryRole || 'engineer';

                  if (!(user as any).role) {
                     await db.update(hbSchema.user)
                        .set({ role: assignedRole })
                        .where(eq(hbSchema.user.id, user.id))
                        .catch(() => {});
                  }

                  if (matchedDirUser?.securityGroupIds?.length) {
                     await syncUserSecurityGroupsToOrganizations(user.id, matchedDirUser.securityGroupIds).catch(() => {});
                  }

                  const provider = createDrizzleEventsProvider();
                  await provider.ingest({
                     id: crypto.randomUUID(),
                     type: "user.joined",
                     timestamp: new Date(),
                     status: "success",
                     userId: user.id,
                     metadata: { email: user.email, name: user.name, role: assignedRole, ssoProvider: "microsoft-entra-id" },
                     source: "app",
                     display: {
                        message: `Identity enrolled: ${user.email} (${assignedRole})`,
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
