import { auth } from "./server/utils/auth";

async function init() {
    console.log("Initializing Better Auth database tables...");
    try {
        // This will attempt to create the tables if they don't exist
        await auth.init();
        console.log("Database initialized successfully.");
    } catch (e) {
        console.error("Initialization failed:", e);
    }
    process.exit(0);
}

init();
