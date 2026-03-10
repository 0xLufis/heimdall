import { auth, pool } from "../../utils/auth";

export default defineEventHandler(async (event) => {
    // Only allow this in development
    if (process.env.NODE_ENV !== 'development') {
        throw createError({
            statusCode: 403,
            statusMessage: 'Forbidden in production'
        });
    }

    const adminEmail = "admin";
    const adminPassword = "admin"; 

    try {
        console.log("Testing direct DB connection via pool...");
        const testResult = await pool.query('SELECT NOW()');
        console.log("DB Connection OK:", testResult.rows[0]);

        console.log("Ensuring System Admin exists...");
        // Check if user already exists
        const existingUserRes = await pool.query('SELECT id FROM "user" WHERE email = $1', [adminEmail]);
        
        let adminId = "";
        if (existingUserRes.rows.length > 0) {
            adminId = existingUserRes.rows[0].id;
            console.log("Admin user exists, ensuring username and role are set...");
            await pool.query('UPDATE "user" SET username = $1, role = $2 WHERE id = $3', [adminEmail, "admin", adminId]);
        } else {
            console.log("Creating admin user via Better Auth first...");
            const dummyEmail = "admin@temp.dev";
            const user = await auth.api.signUpEmail({
                body: {
                    email: dummyEmail,
                    password: adminPassword,
                    name: "System Administrator",
                }
            });
            adminId = user.user.id;
            await pool.query('UPDATE "user" SET email = $1, username = $2, role = $3 WHERE id = $4', [adminEmail, adminEmail, "admin", adminId]);
        }

        console.log("Seeding Mock Organizations...");
        const mockOrgs = [
            { name: "Heimdall Engineering", slug: "engineering" },
            { name: "Plant Operations", slug: "plant-ops" },
            { name: "External Contractors", slug: "external" }
        ];

        for (const org of mockOrgs) {
            const existingOrg = await pool.query('SELECT id FROM organization WHERE slug = $1', [org.slug]);
            if (existingOrg.rows.length === 0) {
                console.log(`Creating Organization: ${org.name}`);
                // Use Better Auth API or direct SQL
                // Direct SQL is easier for mass seeding without needing a session
                const orgId = Math.random().toString(36).substring(2, 15);
                await pool.query(
                    'INSERT INTO organization (id, name, slug, "createdAt") VALUES ($1, $2, $3, NOW())',
                    [orgId, org.name, org.slug]
                );
                // Link admin to these orgs
                await pool.query(
                    'INSERT INTO member (id, "organizationId", "userId", role, "createdAt") VALUES ($1, $2, $3, $4, NOW())',
                    [Math.random().toString(36).substring(2, 15), orgId, adminId, "admin"]
                );
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
            const existingUser = await pool.query('SELECT id FROM "user" WHERE email = $1', [mu.email]);
            if (existingUser.rows.length === 0) {
                console.log(`Creating User: ${mu.name}`);
                try {
                    const user = await auth.api.signUpEmail({
                        body: {
                            email: mu.email,
                            password: "Password123!",
                            name: mu.name,
                        }
                    });
                    await pool.query('UPDATE "user" SET role = $1 WHERE id = $2', [mu.role, user.user.id]);
                } catch (e) {
                    console.error(`Failed to seed user ${mu.name}:`, e);
                }
            }
        }

        return {
            message: "Seed complete.",
            admin: adminEmail,
            orgsCreated: mockOrgs.length,
            usersCreated: mockUsers.length,
            instructions: "Visit /auth/login. Admin credentials: admin / admin. Mock users password: Password123!"
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
