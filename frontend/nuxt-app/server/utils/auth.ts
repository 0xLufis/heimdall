import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { admin, username, organization, multiSession } from "better-auth/plugins";
import { useDb } from "./db"; // your drizzle instance
import * as hbSchema from "../database/drizzle/schema"

export const auth = betterAuth({
   secret: process.env.BETTER_AUTH_SECRET,
   baseURL: process.env.BETTER_AUTH_URL || "http://localhost:3000",
   onRequest: (context) => {
      console.log("Better Auth request:", context.request.url);
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
   socialProviders: {
      github: {
         clientId: process.env.GITHUB_CLIENT_ID as string,
         clientSecret: process.env.GITHUB_CLIENT_SECRET as string,
      },
      google: {
         clientId: process.env.GOOGLE_CLIENT_ID as string,
         clientSecret: process.env.GOOGLE_CLIENT_SECRET as string,
      },
      microsoft: {
         clientId: process.env.MICROSOFT_ENTRA_ID_CLIENT_ID as string,
         clientSecret: process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET as string,
         tenantId: process.env.MICROSOFT_ENTRA_ID_TENANT_ID as string,
      },
   },
});
