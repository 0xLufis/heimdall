import { defineConfig } from 'drizzle-kit';

export default defineConfig({
   dialect: 'postgresql',
   schema: './server/database/drizzle/schema.ts',
   out: './server/database/drizzle',
   dbCredentials: {
      url: process.env.DATABASE_URL!,
   },

   schemaFilter: ['auth'],
   migrations: {
      schema: 'auth',
      table: '__drizzle_migrations',
   }
});
