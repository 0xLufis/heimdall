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
import { adminEscalationGuard } from "./adminGuard";
import { evictBackendSessionCache, evictBackendSessionCacheForUser, resolveDefaultOrganizationId } from "./sessionSync";
import { DEFAULT_ROLE, HEIMDALL_ROLES, USER_ADMIN_ROLES, canManageOrganizations } from "../../app/utils/roles";

// Better-Auth access control for every Heimdall role (app/utils/roles.ts mirrors the backend's
// HeimdallRoles). The previous hand-written map knew only 10 names - one of them, `team_lead`,
// unknown to the backend - so assigning e.g. `group_leader` or `plant_director` was rejected.
const userAdminRoles: readonly string[] = USER_ADMIN_ROLES;
const adminPluginRoles = Object.fromEntries(
   HEIMDALL_ROLES.map((role) => [role, userAdminRoles.includes(role) ? adminAc : userAc])
);

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
      || "00000000-de70-4e1d-8000-00000000c0de"; // fictitious demo tenant; set AZURE_AD_TENANT_ID for real SSO

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
   hooks: {
      // Stops user administrators from escalating through /api/auth/admin/*, and requires the
      // OrganizationManagement right for the organization plugin's mutations (see adminGuard.ts).
      before: adminEscalationGuard
   },
   databaseHooks: {
      session: {
         create: {
            // Start every session in one of the user's organizations. Better-Auth leaves
            // activeOrganizationId empty until the client calls organization.setActive (which the
            // UI never does), so the backend issued no OrgId claim and non-administrators saw no
            // tenant-owned data after a normal sign-in.
            before: async (session) => {
               if ((session as any).activeOrganizationId) return;
               const activeOrganizationId = await resolveDefaultOrganizationId(session.userId);
               if (!activeOrganizationId) return;
               return { data: { ...session, activeOrganizationId } };
            },
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
         },
         // The backend caches role and active organization per session token: tell it when a
         // session changes (organization switch) or disappears (sign-out, revocation, ban).
         update: {
            after: async (session) => {
               await evictBackendSessionCache([(session as any)?.token]);
            }
         },
         delete: {
            after: async (session) => {
               await evictBackendSessionCache([(session as any)?.token]);
            }
         }
      },
      user: {
         // Role, ban or profile change: every session of that user must be re-read by the backend.
         update: {
            after: async (user) => {
               await evictBackendSessionCacheForUser((user as any)?.id);
            }
         },
         create: {
            after: async (user) => {
               try {
                  const db = useDb();
                  const plantUsers = getPlantUsers();
                  const matchedDirUser = plantUsers.find(u => u.email.toLowerCase() === (user.email || '').toLowerCase());
                  const assignedRole = (user as any).role || matchedDirUser?.primaryRole || DEFAULT_ROLE;

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
         role: { type: "string" },
         // Explicit per-user grants (comma-separated, e.g. "organizations.manage"). Never an input of
         // sign-up / update-user; changed through PUT /api/users/:id/permissions.
         permissions: { type: "string", required: false, input: false }
      }
   },
   plugins: [
      admin({
         adminRoles: [...USER_ADMIN_ROLES],
         defaultRole: DEFAULT_ROLE,
         roles: adminPluginRoles
      }),
      username(),
      organization({
         // Organizations are tenant boundaries: only holders of the OrganizationManagement right
         // (Heimdall admins, middle managers and up, or the explicit grant) create them. Better-Auth's
         // default lets any signed-in user. The UI creates through POST /api/organizations.
         allowUserToCreateOrganization: (user) => canManageOrganizations((user as any).role, (user as any).permissions),
         organizationHooks: {
            // Membership decides whether the backend honours a session's active organization.
            afterAddMember: async ({ member }) => { await evictBackendSessionCacheForUser(member?.userId); },
            afterRemoveMember: async ({ member }) => { await evictBackendSessionCacheForUser(member?.userId); },
            afterUpdateMemberRole: async ({ member }) => { await evictBackendSessionCacheForUser(member?.userId); }
         }
      }),
      multiSession()
   ],
   emailAndPassword: {
      enabled: true,
   },
   ...(Object.keys(socialProvidersConfig).length > 0 ? { socialProviders: socialProvidersConfig } : {})
});
