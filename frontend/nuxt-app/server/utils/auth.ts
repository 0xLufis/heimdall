import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { admin, username, organization, multiSession } from "better-auth/plugins";
import { dash } from "@better-auth/infra";
import { useDb } from "./db"; // your drizzle instance
import * as hbSchema from "../database/drizzle/schema"

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
      tenantId: process.env.MICROSOFT_ENTRA_ID_TENANT_ID
   };
}

export const auth = betterAuth({
   secret: process.env.BETTER_AUTH_SECRET || "heimdall-local-dev-secret-key-32-chars-min-security",
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
   user: {
      additionalFields: {
         role: { type: "string" }
      }
   },
   plugins: [
      admin(),
      username(),
      organization(),
      multiSession()
   ],
   emailAndPassword: {
      enabled: true,
   },
   ...(Object.keys(socialProvidersConfig).length > 0 ? { socialProviders: socialProvidersConfig } : {})
});
