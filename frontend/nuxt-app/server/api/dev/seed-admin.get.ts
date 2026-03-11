import { useDb } from "../../utils/db"
import { eq, sql } from "drizzle-orm"
import { user, organization, member } from "../../database/drizzle/schema"
import { auth } from "../../utils/auth"

export default defineEventHandler(async (event) => {
   // Only allow this in development
   if (process.env.NODE_ENV !== 'development') {
      throw createError({
         statusCode: 403,
         statusMessage: 'Forbidden in production'
      });
   }

   const db = useDb();
   const adminEmail = "admin@heimdall.dev";
   const adminPassword = "AdminPassword123!";
   const adminUsername = "admin";

   try {
      console.log("Testing direct DB connection via useDb...");
      const testResult = await db.execute(sql`SELECT NOW()`);
      console.log("DB Connection OK:", testResult[0]);

      console.log("Ensuring System Admin exists...");
      // Check if user already exists
      const existingUserRes = await db.select().from(user).where(eq(user.email, adminEmail));

      let adminId = "";
      if (existingUserRes.length > 0) {
         adminId = existingUserRes[0].id;
         console.log("Admin user exists, ensuring username and role are set...");
         await db.update(user)
            .set({ username: adminUsername, role: "admin" })
            .where(eq(user.id, adminId));
      } else {
         console.log("Creating admin user via Better Auth first...");
         let newUser;
         try {
            newUser = await auth.api.signUpEmail({
               body: {
                  email: adminEmail,
                  password: adminPassword,
                  name: "System Administrator",
               }
            });
         } catch (e: any) {
            console.error("signUpEmail failed. Error object:", JSON.stringify(e, null, 2));
            console.error("Error message:", e.message);
            throw e;
         }
         adminId = newUser.user.id;
         
         await db.update(user)
            .set({ username: adminUsername, role: "admin" })
            .where(eq(user.id, adminId));
      }

      console.log("Seeding Mock Organizations...");
      const mockOrgs = [
         { name: "Heimdall Engineering", slug: "engineering" },
         { name: "Plant Operations", slug: "plant-ops" },
         { name: "External Contractors", slug: "external" }
      ];

      for (const org of mockOrgs) {
         const existingOrg = await db.select().from(organization).where(eq(organization.slug, org.slug));
         if (existingOrg.length === 0) {
            console.log(`Creating Organization: ${org.name}`);
            const orgId = Math.random().toString(36).substring(2, 15);
            await db.insert(organization).values({
               id: orgId,
               name: org.name,
               slug: org.slug,
               createdAt: new Date()
            });
            
            // Link admin to these orgs
            await db.insert(member).values({
               id: Math.random().toString(36).substring(2, 15),
               organizationId: orgId,
               userId: adminId,
               role: "admin",
               createdAt: new Date()
            });
         }
      }

      console.log("Seeding Mock Users...");
      const mockUsers = [
         { name: "Jane Engineer", email: "jane@heimdall.dev", role: "engineer" },
         { name: "Bob Manager", email: "bob@heimdall.dev", role: "manager" },
         { name: "Alice Technician", email: "alice@heimdall.dev", role: "technician" },
         { name: "System Bot", email: "bot@heimdall.dev", role: "generic" }
      ];

      for (const mu of mockUsers) {
         const existingUser = await db.select().from(user).where(eq(user.email, mu.email));
         if (existingUser.length === 0) {
            console.log(`Creating User: ${mu.name}`);
            try {
               const newUser = await auth.api.signUpEmail({
                  body: {
                     email: mu.email,
                     password: "MockPassword123!",
                     name: mu.name,
                  }
               });
               await db.update(user)
                  .set({ role: mu.role, username: mu.name.split(' ')[0].toLowerCase() })
                  .where(eq(user.id, newUser.user.id));
            } catch (e) {
               console.error(`Failed to seed user ${mu.name}:`, e);
            }
         }
      }

      return {
         message: "Seed complete.",
         admin: adminEmail,
         adminUsername: adminUsername,
         orgsCreated: mockOrgs.length,
         usersCreated: mockUsers.length,
         instructions: "Visit /auth/login. Admin credentials: admin@heimdall.dev / AdminPassword123! OR username: admin / AdminPassword123!. Mock users password: MockPassword123!"
      };
   } catch (error: any) {
      console.error("Seeding error stack:", error);
      return {
         error: "Failed to seed data",
         message: error.message,
         details: error.toString()
      };
   }
});
