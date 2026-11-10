import type { StudioConfig } from "better-auth-studio";
import { auth } from "./server/utils/auth";
import { useDb } from "./server/utils/db";
import { createDrizzleEventsProvider } from "./server/utils/studioEventsProvider";

const db = useDb();
const eventsProvider = createDrizzleEventsProvider();

const config: StudioConfig = {
  auth,
  basePath: process.env.STUDIO_BASE_PATH || "/admin/studio",
  metadata: {
    title: "Heimdall Identity Studio",
    company: {
      name: "Heimdall Industrial Security",
      website: "http://localhost:3000",
    },
    theme: "dark",
  },
  access: {
    roles: ["system_admin", "heimdall_admin", "admin", "it_admin"],
    ...(process.env.STUDIO_ALLOWED_IPS ? {
      allowIpAddresses: process.env.STUDIO_ALLOWED_IPS.split(",").map((s) => s.trim()),
    } : {}),
  },
  events: {
    enabled: true,
    provider: eventsProvider,
    client: db,
    clientType: "drizzle",
    tableName: "auth_events",
    schema: "auth",
    liveMarquee: {
      enabled: true,
      pollInterval: 2000,
    },
  },
};

export default config;
