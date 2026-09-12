import { defineNitroPlugin } from 'nitropack/runtime'
import { useDb } from '../utils/db'
import { eq } from 'drizzle-orm'
import { user, account } from '../database/drizzle/schema'
import { auth } from '../utils/auth'

export default defineNitroPlugin(async () => {
  if (process.env.NODE_ENV !== 'development') {
    return
  }

  const adminEmail = "admin@heimdall.dev"
  const adminPassword = "AdminPassword123!"
  const adminUsername = "admin"

  try {
    const db = useDb()
    const existingUser = await db.select().from(user).where(eq(user.email, adminEmail))
    
    if (existingUser.length === 0) {
      console.log("[ensureAdminUser] Admin user not found. Auto-seeding admin@heimdall.dev...")
      const newUser = await auth.api.signUpEmail({
        body: {
          email: adminEmail,
          password: adminPassword,
          name: "System Administrator"
        }
      })
      await db.update(user)
        .set({ username: adminUsername, role: "admin" })
        .where(eq(user.id, newUser.user.id))
      console.log("[ensureAdminUser] Admin user created successfully.")
    } else {
      const adminId = existingUser[0].id
      const existingAccount = await db.select().from(account).where(eq(account.userId, adminId))
      if (existingAccount.length === 0) {
        console.log("[ensureAdminUser] Admin account credential missing. Re-creating...")
        await db.delete(user).where(eq(user.id, adminId))
        const newUser = await auth.api.signUpEmail({
          body: {
            email: adminEmail,
            password: adminPassword,
            name: "System Administrator"
          }
        })
        await db.update(user)
          .set({ username: adminUsername, role: "admin" })
          .where(eq(user.id, newUser.user.id))
        console.log("[ensureAdminUser] Admin user & credential re-created successfully.")
      } else if (!existingUser[0].username || existingUser[0].role !== "admin") {
        await db.update(user)
          .set({ username: adminUsername, role: "admin" })
          .where(eq(user.id, adminId))
      }
    }
  } catch (err) {
    console.warn("[ensureAdminUser] Note: could not auto-verify admin user at boot:", err)
  }
})
