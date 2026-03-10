import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { useDb } from "../../utils/db"; // your drizzle instance

export const auth = betterAuth({
   database: drizzleAdapter(useDb, {
      provider: "pg", // or "mysql", "sqlite"
   }),


});
