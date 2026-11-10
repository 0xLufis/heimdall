import { eq } from 'drizzle-orm'
import { useDb } from './db'
import * as hbSchema from '../database/drizzle/schema'
import { getPlantUsers } from './datasetLoader'

export interface AuthUserProfile {
  id: string
  name: string
  email: string
  role: string
  username?: string
  department?: string
  image?: string
}

/**
 * Master catalog of all legacy, ticketing, technician, and persona user profiles.
 * Every user referenced anywhere in Heimdall must have a verified Better-Auth user profile.
 */
export const MASTER_SYSTEM_USER_PROFILES: AuthUserProfile[] = [
  // Primary Administrator
  {
    id: 'usr-admin-primary',
    name: 'System Administrator',
    email: 'admin@heimdall.dev',
    role: 'system_admin',
    username: 'admin',
    department: 'Platform Operations'
  },
  {
    id: 'usr-admin',
    name: 'System Administrator',
    email: 'admin@heimdall.local',
    role: 'system_admin',
    username: 'sysadmin',
    department: 'Platform Operations'
  },
  {
    id: 'dev-admin-id',
    name: 'Dev Administrator',
    email: 'admin@heimdall.local',
    role: 'system_admin',
    username: 'dev_admin',
    department: 'Platform Operations'
  },

  // Demo Personas (from useAuthSession.ts)
  {
    id: 'usr-sysadmin-1',
    name: 'Root System Administrator',
    email: 'sysadmin@heimdall.dev',
    role: 'system_admin',
    username: 'sysadmin_1',
    department: 'Platform Operations'
  },
  {
    id: 'usr-director-1',
    name: 'Dr. Henrik Weber (Plant Director)',
    email: 'henrik.weber@factory.corp',
    role: 'plant_director',
    username: 'henrik_weber',
    department: 'Plant Management'
  },
  {
    id: 'usr-plant-eng-1',
    name: 'Gábor Varga (Plant Engineering Manager)',
    email: 'gabor.varga@factory.corp',
    role: 'plant_engineering_manager',
    username: 'gabor_varga_mgr',
    department: 'Plant Engineering'
  },
  {
    id: 'usr-senior-eng-1',
    name: 'Elena Rostova (Senior Engineering Manager)',
    email: 'elena.rostova@factory.corp',
    role: 'senior_engineering_manager',
    username: 'elena_rostova',
    department: 'Engineering Operations'
  },
  {
    id: 'usr-heimdall-admin-1',
    name: 'Heimdall Administrator',
    email: 'admin.platform@heimdall.dev',
    role: 'heimdall_admin',
    username: 'heimdall_admin_1',
    department: 'Platform Administration'
  },
  {
    id: 'usr-itadmin-1',
    name: 'IT Infrastructure Administrator',
    email: 'it.admin@heimdall.dev',
    role: 'it_admin',
    username: 'it_admin',
    department: 'Enterprise IT'
  },
  {
    id: 'usr-itsiteadmin-1',
    name: 'Marcus Vance (IT Site Administrator)',
    email: 'marcus.vance@factory.corp',
    role: 'it_site_admin',
    username: 'marcus_vance',
    department: 'Industrial IT'
  },
  {
    id: 'usr-engadmin-1',
    name: 'Engineering Administrator',
    email: 'eng.admin@heimdall.dev',
    role: 'engineering_admin',
    username: 'eng_admin',
    department: 'Functional Engineering Systems'
  },
  {
    id: 'usr-manager-andras',
    name: 'András Molnár (Plant Manager)',
    email: 'andras.manager@heimdall.dev',
    role: 'manager',
    username: 'andras_manager',
    department: 'Plant Operations'
  },
  {
    id: 'usr-op-planner-1',
    name: 'András Molnár (Operative Planner)',
    email: 'andras.planner@factory.corp',
    role: 'operative_planner',
    username: 'andras_planner',
    department: 'Line Operations Planning'
  },
  {
    id: 'usr-gl-assy-1',
    name: 'Sally Vance (Group Leader – Fastening & Assembly)',
    email: 'sally.vance@factory.corp',
    role: 'group_leader',
    username: 'sally_vance',
    department: 'Fastening & Assembly'
  },
  {
    id: 'usr-ferenc',
    name: 'Shift Leader Ferenc',
    email: 'ferenc.leader@heimdall.dev',
    role: 'shift_leader',
    username: 'ferenc_leader',
    department: 'Shift Operations'
  },
  {
    id: 'usr-eng-robotics-1',
    name: 'Alex Novak (Robotics & SMT Controls Engineer)',
    email: 'alex.novak@factory.corp',
    role: 'engineer',
    username: 'alex_novak',
    department: 'Automation & Controls'
  },
  {
    id: 'usr-tech-welding-1',
    name: 'István Kovács (Laser Welding & Mechanical Tech)',
    email: 'istvan.kovacs@factory.corp',
    role: 'technician',
    username: 'istvan_kovacs_weld',
    department: 'Laser Welding Maintenance'
  },

  // Seeded Candidates & Dedication Users (from technicianRulesStore.ts)
  {
    id: 'usr-sally',
    name: 'Engineer Sally',
    email: 'sally.milling@heimdall.dev',
    role: 'engineer',
    username: 'sally_milling',
    department: 'Milling & Machining'
  },
  {
    id: 'usr-orwell',
    name: 'Engineer Orwell',
    email: 'orwell.audi@heimdall.dev',
    role: 'group_leader',
    username: 'orwell_audi',
    department: 'Battery Assembly (AUDI)'
  },
  {
    id: 'usr-katalin',
    name: 'Katalin Nagy',
    email: 'katalin.aoi@heimdall.dev',
    role: 'group_leader',
    username: 'katalin_aoi',
    department: 'Quality & Vision AI'
  },
  {
    id: 'usr-kovacs',
    name: 'István Kovács',
    email: 'istvan.kovacs@heimdall.dev',
    role: 'technician',
    username: 'istvan_kovacs',
    department: 'Line 06 Mechanical'
  },
  {
    id: 'usr-varga',
    name: 'Gábor Varga',
    email: 'gabor.varga@heimdall.dev',
    role: 'technician',
    username: 'gabor_varga',
    department: 'General Maintenance'
  },
  {
    id: 'usr-nemeth',
    name: 'Zoltán Németh',
    email: 'zoltan.nemeth@heimdall.dev',
    role: 'technician',
    username: 'zoltan_nemeth',
    department: 'Robotics & Fastening'
  },
  {
    id: 'usr-horvath',
    name: 'Bence Horváth',
    email: 'bence.horvath@heimdall.dev',
    role: 'technician',
    username: 'bence_horvath',
    department: 'Pressing & Fitting'
  },
  {
    id: 'usr-pap',
    name: 'Orsolya Pap',
    email: 'orsolya.pap@heimdall.dev',
    role: 'engineer',
    username: 'orsolya_pap',
    department: 'Testing & Diagnostics'
  },

  // Ticketing Users (from initialTickets.ts)
  {
    id: 'usr-op-01',
    name: 'István Kovács (Operator)',
    email: 'istvan.kovacs.op@factory.corp',
    role: 'operator',
    username: 'op_kovacs',
    department: 'Machining Operations'
  },
  {
    id: 'usr-tech-01',
    name: 'Gábor Varga (Lead Tech)',
    email: 'gabor.varga.lead@factory.corp',
    role: 'technician',
    username: 'tech_varga',
    department: 'Plant Maintenance'
  },
  {
    id: 'usr-op-02',
    name: 'Zoltán Horváth',
    email: 'zoltan.horvath.op@factory.corp',
    role: 'operator',
    username: 'op_horvath',
    department: 'Laser Welding Operations'
  },
  {
    id: 'usr-tech-02',
    name: 'Zoltán Németh',
    email: 'zoltan.nemeth.tech@factory.corp',
    role: 'technician',
    username: 'tech_nemeth',
    department: 'Plant Maintenance'
  },
  {
    id: 'usr-op-03',
    name: 'Péter Nagy',
    email: 'peter.nagy.op@factory.corp',
    role: 'operator',
    username: 'op_nagy',
    department: 'Dispensing Operations'
  },
  {
    id: 'usr-op-04',
    name: 'Tamás Szabó',
    email: 'tamas.szabo.op@factory.corp',
    role: 'operator',
    username: 'op_szabo',
    department: 'Press Fit Operations'
  },
  {
    id: 'usr-tech-03',
    name: 'Bence Horváth',
    email: 'bence.horvath.tech@factory.corp',
    role: 'technician',
    username: 'tech_bence',
    department: 'Plant Maintenance'
  },
  {
    id: 'usr-tech-04',
    name: 'Orsolya Pap',
    email: 'orsolya.pap.tech@factory.corp',
    role: 'technician',
    username: 'tech_pap',
    department: 'Testing Operations'
  },
  {
    id: 'usr-op-05',
    name: 'László Kiss',
    email: 'laszlo.kiss.op@factory.corp',
    role: 'operator',
    username: 'op_kiss',
    department: 'Safety Operations'
  },
  {
    id: 'usr-op-06',
    name: 'Balázs Farkas',
    email: 'balazs.farkas.op@factory.corp',
    role: 'operator',
    username: 'op_farkas',
    department: 'Conveyor Operations'
  },

  // Legacy Backend Technician Candidates (tech-01 to tech-05, tech-alice, tech-sally, tech-orwell, tech-kovacs)
  {
    id: 'tech-01',
    name: 'Kovács István',
    email: 'i.kovacs@heimdall.local',
    role: 'technician',
    username: 'tech_01',
    department: 'Mechanical Maintenance'
  },
  {
    id: 'tech-02',
    name: 'Nagy Péter',
    email: 'p.nagy@heimdall.local',
    role: 'technician',
    username: 'tech_02',
    department: 'Electrical Engineering'
  },
  {
    id: 'tech-03',
    name: 'Szabó Tamás',
    email: 't.szabo@heimdall.local',
    role: 'controls_engineer',
    username: 'tech_03',
    department: 'Controls Engineering'
  },
  {
    id: 'tech-04',
    name: 'Varga Zoltán',
    email: 'z.varga@heimdall.local',
    role: 'engineer',
    username: 'tech_04',
    department: 'Robotics Automation'
  },
  {
    id: 'tech-05',
    name: 'Tóth Bence',
    email: 'b.toth@heimdall.local',
    role: 'lead_engineer',
    username: 'tech_05',
    department: 'Plant Maintenance'
  },
  {
    id: 'tech-alice',
    name: 'Alice Engineer',
    email: 'alice.engineer@heimdall.local',
    role: 'technician',
    username: 'tech_alice',
    department: 'Automation Engineering'
  },
  {
    id: 'tech-sally',
    name: 'Engineer Sally',
    email: 'sally.tech@heimdall.dev',
    role: 'engineer',
    username: 'tech_sally',
    department: 'Milling & Machining'
  },
  {
    id: 'tech-orwell',
    name: 'Engineer Orwell',
    email: 'orwell.tech@heimdall.dev',
    role: 'group_leader',
    username: 'tech_orwell',
    department: 'Battery Assembly'
  },
  {
    id: 'tech-kovacs',
    name: 'István Kovács',
    email: 'kovacs.tech@heimdall.dev',
    role: 'technician',
    username: 'tech_kovacs',
    department: 'Mechanical Maintenance'
  },

  // Automation & Agent Bots
  {
    id: 'usr-automation-bot',
    name: 'Outside IT Automation Bot',
    email: 'it-automation@heimdall.local',
    role: 'system_admin',
    username: 'automation_bot',
    department: 'Outside IT Automation'
  },
  {
    id: 'outside-it-automation-bot',
    name: 'Outside IT Automation Bot',
    email: 'it-automation-service@heimdall.local',
    role: 'system_admin',
    username: 'outside_it_bot',
    department: 'Enterprise IT Workflow'
  },
  {
    id: 'usr-autonomous-agent',
    name: 'Autonomous Fleet Telemetry (Dev)',
    email: 'telemetry-agent@heimdall.local',
    role: 'technician',
    username: 'autonomous_agent',
    department: 'Edge Daemon Ingestion'
  }
]

/** In-memory cache of resolved Better-Auth user profiles */
const inMemoryProfilesMap = new Map<string, AuthUserProfile>()

// Pre-fill memory cache with master system profiles
for (const p of MASTER_SYSTEM_USER_PROFILES) {
  inMemoryProfilesMap.set(p.id.toLowerCase(), p)
  inMemoryProfilesMap.set(p.email.toLowerCase(), p)
  if (p.username) inMemoryProfilesMap.set(p.username.toLowerCase(), p)
}

/**
 * Returns all known Better-Auth user profiles, including 60 enterprise plant dataset users.
 */
export function getAllKnownUserProfiles(): AuthUserProfile[] {
  const result: AuthUserProfile[] = [...MASTER_SYSTEM_USER_PROFILES]
  try {
    const plantUsers = getPlantUsers()
    for (const pu of plantUsers) {
      if (!result.some(r => r.id.toLowerCase() === pu.id.toLowerCase())) {
        result.push({
          id: pu.id,
          name: pu.name,
          email: pu.email,
          role: pu.primaryRole,
          username: pu.username,
          department: pu.department
        })
      }
    }
  } catch {}
  return result
}

/**
 * Resolves or builds a compliant Better-Auth profile for any given user ID or identifier.
 */
export function resolveUserProfile(userId: string, hints?: { name?: string; email?: string; role?: string }): AuthUserProfile {
  const key = (userId || '').trim().toLowerCase()
  if (inMemoryProfilesMap.has(key)) {
    const existing = inMemoryProfilesMap.get(key)!
    return {
      ...existing,
      name: hints?.name || existing.name,
      email: hints?.email || existing.email,
      role: hints?.role || existing.role
    }
  }

  // Check plant users from dataset
  try {
    const plantUsers = getPlantUsers()
    const matched = plantUsers.find(u => u.id.toLowerCase() === key || u.email.toLowerCase() === key || u.username.toLowerCase() === key)
    if (matched) {
      const profile: AuthUserProfile = {
        id: matched.id,
        name: hints?.name || matched.name,
        email: hints?.email || matched.email,
        role: hints?.role || matched.primaryRole,
        username: matched.username,
        department: matched.department
      }
      inMemoryProfilesMap.set(key, profile)
      return profile
    }
  } catch {}

  // Fallback synthesis ensuring compliant Better-Auth user profile
  const synthesizedName = hints?.name || (userId.startsWith('usr-') ? userId.replace('usr-', '').replace(/[-_]/g, ' ') : userId)
  const synthesizedEmail = hints?.email || (userId.includes('@') ? userId : `${userId.toLowerCase()}@factory.corp`)
  const synthesizedRole = hints?.role || 'engineer'

  const newProfile: AuthUserProfile = {
    id: userId,
    name: synthesizedName,
    email: synthesizedEmail,
    role: synthesizedRole,
    username: userId.toLowerCase().replace(/[^a-z0-9_]/g, '_')
  }

  inMemoryProfilesMap.set(key, newProfile)
  return newProfile
}

/**
 * Asserts and ensures that a user exists with a full profile in the Better-Auth user table (`auth.user`).
 * If the user does not exist in the database, inserts them automatically.
 */
export async function ensureBetterAuthProfile(
  userId: string,
  hints?: { name?: string; email?: string; role?: string }
): Promise<AuthUserProfile> {
  const profile = resolveUserProfile(userId, hints)

  try {
    const db = useDb()
    const existing = await db
      .select({ id: hbSchema.user.id })
      .from(hbSchema.user)
      .where(eq(hbSchema.user.id, profile.id))
      .limit(1)

    if (existing.length === 0) {
      await db.insert(hbSchema.user).values({
        id: profile.id,
        name: profile.name,
        email: profile.email,
        emailVerified: true,
        role: profile.role,
        username: profile.username || profile.email.split('@')[0],
        createdAt: new Date(),
        updatedAt: new Date()
      }).onConflictDoUpdate({
        target: hbSchema.user.id,
        set: {
          name: profile.name,
          email: profile.email,
          role: profile.role,
          updatedAt: new Date()
        }
      })
    }
  } catch (err) {
    // Database offline or SQLite/mock mode — inMemory profile is guaranteed
  }

  return profile
}

/**
 * Synchronizes all known master profiles into the Better-Auth database table.
 */
export async function ensureAllBetterAuthProfiles(): Promise<number> {
  const all = getAllKnownUserProfiles()
  let count = 0

  try {
    const db = useDb()
    for (const p of all) {
      try {
        await db.insert(hbSchema.user).values({
          id: p.id,
          name: p.name,
          email: p.email,
          emailVerified: true,
          role: p.role,
          username: p.username || p.email.split('@')[0],
          createdAt: new Date(),
          updatedAt: new Date()
        }).onConflictDoUpdate({
          target: hbSchema.user.id,
          set: {
            name: p.name,
            email: p.email,
            role: p.role,
            updatedAt: new Date()
          }
        })
        count++
      } catch (insertErr) {
        // Individual insert error
      }
    }
  } catch (err) {
    console.warn('[authProfiles] Database unavailable for bulk sync:', err)
  }

  return count
}
