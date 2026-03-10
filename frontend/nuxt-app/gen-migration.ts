import { auth } from "./server/utils/auth";
import { getMigrations } from "better-auth/db/migration";

async function generate() {
   const migration = await getMigration(auth);
   console.log(migration.compile);
   process.exit(0);
}

generate();
