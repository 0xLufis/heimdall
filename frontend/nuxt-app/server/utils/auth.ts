import { betterAuth } from "better-auth";
import pg from "pg";

// Extract the Pool class from the pg package
const { Pool } = pg;

// Create a connection pool using your environment variable
const pool = new Pool({
   connectionString: process.env.DATABASE_URL,
});

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
   }
});
