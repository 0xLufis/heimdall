import { defineNitroPlugin } from 'nitropack/runtime'
import { useDb } from '../utils/db'
import { eq } from 'drizzle-orm'
import { user, account } from '../database/drizzle/schema'
import { auth } from '../utils/auth'
import { featureFlags } from '../utils/featureFlags'
import { ensureAllBetterAuthProfiles } from '../utils/authProfiles'

export default defineNitroPlugin(async () => {
  if (!featureFlags.enableDevFeatures) {
    return
  }

  const adminEmail = "admin@heimdall.dev"
  const adminPassword = "AdminPassword123!"
  const adminUsername = "admin"
  // The bootstrap administrator is the platform's `system_admin`, as in the seed
  // (seed_data/seed_pipeline.py) and the master profile catalogue. It used to be created as the
  // legacy `admin` role, which is a platform administrator but fails the backend's
  // ItAdministration, EngineeringAdministration and LineStopRequest policies - so this plugin and
  // the seed kept overwriting each other and the result depended on which ran last.
  const adminRole = "system_admin"

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
        .set({ username: adminUsername, role: adminRole })
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
          .set({ username: adminUsername, role: adminRole })
          .where(eq(user.id, newUser.user.id))
        console.log("[ensureAdminUser] Admin user & credential re-created successfully.")
      } else if (!existingUser[0].username || !existingUser[0].role || existingUser[0].role === "admin") {
        // Repair only what is missing or left over from the legacy bootstrap; a role an
        // administrator assigned on purpose is not overwritten on every boot.
        await db.update(user)
          .set({
            username: existingUser[0].username || adminUsername,
            role: !existingUser[0].role || existingUser[0].role === "admin" ? adminRole : existingUser[0].role
          })
          .where(eq(user.id, adminId))
      }
    }

    // Guarantee no user can exist without a Better-Auth user profile
    const count = await ensureAllBetterAuthProfiles()
    console.log(`[ensureBetterAuthProfiles] Verified and synchronized ${count} Better-Auth user profiles.`)
  } catch (err: any) {
    const msg = err?.message || String(err)
    console.warn(`[ensureAdminUser] Database offline at boot (${msg}). Admin auto-verification deferred.`)
  }
})
