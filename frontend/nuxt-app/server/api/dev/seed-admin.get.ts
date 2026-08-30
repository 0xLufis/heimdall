import { useDb } from "../../utils/db"
import { eq, sql } from "drizzle-orm"
import { user, organization, member } from "../../database/drizzle/schema"
import { auth } from "../../utils/auth"

export default defineEventHandler(async (event) => {
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
      console.log("Ensuring System Admin exists...");
      const existingUserRes = await db.select().from(user).where(eq(user.email, adminEmail));

      let adminId = "";
      if (existingUserRes.length > 0) {
         adminId = existingUserRes[0].id;
         await db.update(user)
            .set({ username: adminUsername, role: "admin" })
            .where(eq(user.id, adminId));
      } else {
         let newUser = await auth.api.signUpEmail({
            body: {
               email: adminEmail,
               password: adminPassword,
               name: "System Administrator",
            }
         });
         adminId = newUser.user.id;
         await db.update(user)
            .set({ username: adminUsername, role: "admin" })
            .where(eq(user.id, adminId));
      }

      console.log("Seeding Mock Organizations...");
      const mockOrgs = [
         { name: "Heimdall Engineering", slug: "engineering" },
         { name: "Plant Operations", slug: "plant-ops" },
         { name: "External Contractors", slug: "external" },
         { name: "Logistics & Supply Chain", slug: "logistics" }
      ];

      for (const org of mockOrgs) {
         const existingOrg = await db.select().from(organization).where(eq(organization.slug, org.slug));
         if (existingOrg.length === 0) {
            const orgId = Math.random().toString(36).substring(2, 15);
            await db.insert(organization).values({
               id: orgId,
               name: org.name,
               slug: org.slug,
               createdAt: new Date()
            });
            
            await db.insert(member).values({
               id: Math.random().toString(36).substring(2, 15),
               organizationId: orgId,
               userId: adminId,
               role: "admin",
               createdAt: new Date()
            });
         }
      }

      console.log("Seeding 90 Enterprise Users (40 Engineers, 10 Managers, 39 Technicians, 1 Admin)...");
      const mockUsers: { name: string; email: string; role: string; username: string }[] = [];

      // 40 Engineers
      for (let i = 1; i <= 40; i++) {
         mockUsers.push({
            name: `Engineer ${i}`,
            email: `engineer${i}@heimdall.dev`,
            role: "engineer",
            username: `eng_${i}`
         });
      }

      // 10 Managers
      for (let i = 1; i <= 10; i++) {
         mockUsers.push({
            name: `Manager ${i}`,
            email: `manager${i}@heimdall.dev`,
            role: "manager",
            username: `mgr_${i}`
         });
      }

      // 39 Technicians
      for (let i = 1; i <= 39; i++) {
         mockUsers.push({
            name: `Technician ${i}`,
            email: `technician${i}@heimdall.dev`,
            role: "technician",
            username: `tech_${i}`
         });
      }

      for (const mu of mockUsers) {
         const existingUser = await db.select().from(user).where(eq(user.email, mu.email));
         if (existingUser.length === 0) {
            try {
               const newUser = await auth.api.signUpEmail({
                  body: {
                     email: mu.email,
                     password: "MockPassword123!",
                     name: mu.name,
                  }
               });
               await db.update(user)
                  .set({ role: mu.role, username: mu.username })
                  .where(eq(user.id, newUser.user.id));
            } catch (e) {
               console.error(`Failed to seed user ${mu.name}:`, e);
            }
         }
      }

      return {
         message: "Enterprise Seed complete.",
         totalUsers: 90,
         admin: adminEmail,
         engineers: 40,
         managers: 10,
         technicians: 39,
         orgsCreated: mockOrgs.length
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
