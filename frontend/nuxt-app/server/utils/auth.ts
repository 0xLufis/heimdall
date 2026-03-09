import { betterAuth } from "better-auth";
import { admin, createAccessControl } from "better-auth/plugins";
import pg from "pg";

// Extract the Pool class from the pg package
const { Pool } = pg;

// Create a connection pool using your environment variable
export const pool = new Pool({
   connectionString: process.env.DATABASE_URL,
   ssl: {
      rejectUnauthorized: false
   }
});

const ac = createAccessControl({
   user: ["create", "read", "update", "delete"],
   statement: ["create", "read", "update", "delete"]
});

const roles = {
   system_admin: ac.newRole({
      user: ["create", "read", "update", "delete"],
      statement: ["create", "read", "update", "delete"]
   }),
   admin: ac.newRole({
      user: ["create", "read", "update", "delete"],
      statement: ["create", "read", "update", "delete"]
   }),
   manager: ac.newRole({
      user: ["read", "update"],
      statement: ["read"]
   }),
   team_lead: ac.newRole({
      user: ["read"],
      statement: ["read"]
   }),
   engineer: ac.newRole({
      user: ["read"],
      statement: ["read"]
   }),
   technician: ac.newRole({
      user: ["read"],
      statement: ["read"]
   }),
   generic: ac.newRole({
      user: ["read"],
      statement: ["read"]
   }),
   user: ac.newRole({
      user: ["read"],
      statement: ["read"]
   })
};

export const auth = betterAuth({
   // Tell Better-Auth to use the raw Postgres pool
   database: pool,

   // Define which authentication methods are active
   emailAndPassword: {
      enabled: true,
   },

   // (Optional) Advanced configuration
   session: {
      expiresIn: 60 * 60 * 24 * 7, // 7 days
      updateAge: 60 * 60 * 24, // 1 day (update expiration if active)
   },

   plugins: [
      admin({
         ac,
         roles,
         adminRoles: ["system_admin", "admin"]
      })
   ]
});
