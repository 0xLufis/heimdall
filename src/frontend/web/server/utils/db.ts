import { drizzle } from 'drizzle-orm/postgres-js';
import postgres from 'postgres';
import * as schema from '../database/drizzle/schema'; // Point this to your generated schema

let connection: ReturnType<typeof postgres> | null = null;

export const useDb = () => {
   if (!connection) {
      let configDatabaseUrl = '';
      try {
         configDatabaseUrl = useRuntimeConfig()?.databaseUrl as string;
      } catch {
         // In vitest or standalone context, useRuntimeConfig may not be initialized
      }
      const isDev = process.env.NODE_ENV !== 'production';
      const dbUrl = configDatabaseUrl 
         || process.env.DATABASE_URL 
         || (isDev ? "postgres://nuxt_frontend:your_frontend_pw@localhost:5432/heimdall_dev_db" : undefined);

      if (!dbUrl) {
         throw new Error("CRITICAL SECURITY CONFIGURATION ERROR: DATABASE_URL must be specified via environment variables in production.");
      }

      const useSsl = dbUrl.includes('sslmode=require') || dbUrl.includes('ssl=true');

      connection = postgres(dbUrl, {
         ...(useSsl ? { ssl: { rejectUnauthorized: !isDev } } : {})
      });
   }

   // Return the Drizzle instance bundled with your schema
   return drizzle(connection, { schema });
};
