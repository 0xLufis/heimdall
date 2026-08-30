import { drizzle } from 'drizzle-orm/postgres-js';
import postgres from 'postgres';
import * as schema from '../database/drizzle/schema'; // Point this to your generated schema

let connection: ReturnType<typeof postgres> | null = null;

export const useDb = () => {
   if (!connection) {
      const config = useRuntimeConfig();
      const dbUrl = (config.databaseUrl as string) 
         || process.env.DATABASE_URL 
         || "postgres://ef_admin:migrate@localhost:5432/heimdall_dev_db";

      const useSsl = dbUrl.includes('sslmode=require') || dbUrl.includes('ssl=true');

      connection = postgres(dbUrl, {
         ...(useSsl ? { ssl: { rejectUnauthorized: false } } : {})
      });
   }

   // Return the Drizzle instance bundled with your schema
   return drizzle(connection, { schema });
};
