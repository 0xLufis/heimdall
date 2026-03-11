import { drizzle } from 'drizzle-orm/postgres-js';
import postgres from 'postgres';
import * as schema from '../database/drizzle/schema'; // Point this to your generated schema

let connection: ReturnType<typeof postgres> | null = null;

export const useDb = () => {
   if (!connection) {
      const config = useRuntimeConfig();

      // Initialize the postgres connection
      connection = postgres(config.databaseUrl as string, {
         ssl: {
            rejectUnauthorized: false
         }
      });
   }

   // Return the Drizzle instance bundled with your schema
   return drizzle(connection, { schema });
};
