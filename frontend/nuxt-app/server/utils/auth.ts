import { betterAuth } from "better-auth";
import { admin, createAccessControl, username, organization, multiSession } from "better-auth/plugins";
import pg from "pg";

// We keep the pool for our manual seeder/queries
const Pool = pg.Pool || (pg as any).default?.Pool;
export const pool = new Pool({
   connectionString: process.env.DATABASE_URL,
   ssl: {
      rejectUnauthorized: false
   }
});

const ac = createAccessControl({
   user: ["create", "read", "update", "delete", "list", "impersonate"],
   role: ["create", "read", "update", "delete", "list"],
   session: ["create", "read", "update", "delete", "list"],
   invitation: ["create", "read", "update", "delete", "list"],
   organization: ["create", "read", "update", "delete", "list"],
   member: ["create", "read", "update", "delete", "list"],
   statement: ["create", "read", "update", "delete", "list"]
});

const roles = {
   system_admin: ac.newRole({
      user: ["*"],
      role: ["*"],
      session: ["*"],
      invitation: ["*"],
      organization: ["*"],
      member: ["*"],
      statement: ["*"]
   }),
   admin: ac.newRole({
      user: ["*"],
      role: ["*"],
      session: ["*"],
      invitation: ["*"],
      organization: ["*"],
      member: ["*"],
      statement: ["*"]
   }),
   manager: ac.newRole({
      user: ["read", "update", "list"],
      session: ["read", "list"],
      organization: ["read", "list"],
      member: ["read", "list"],
      statement: ["read"]
   }),
   user: ac.newRole({
      user: ["read"],
      session: ["read"],
      statement: ["read"]
   })
};

export const auth = betterAuth({
   // Pass URL directly so Better Auth uses its own optimized adapter
   database: {
       url: process.env.DATABASE_URL!,
       type: "postgres"
   },
   baseURL: process.env.BETTER_AUTH_URL,
   secret: process.env.BETTER_AUTH_SECRET,

   user: {
      additionalFields: {
         role: { type: "string" },
         username: { type: "string" }
      }
   },
   
   session: {
      expiresIn: 60 * 60 * 24 * 7,
      updateAge: 60 * 60 * 24,
      cookieCache: {
          enabled: true,
          maxAge: 60 * 5
      },
      additionalFields: {
          role: { type: "string" }
      }
   },

   emailAndPassword: {
      enabled: true,
      minPasswordLength: 4,
   },

   plugins: [
      admin({
         ac,
         roles,
         adminRoles: ["system_admin", "admin"]
      }),
      username(),
      organization({
          ac,
          creatorRole: "admin"
      }),
      multiSession()
   ]
});
