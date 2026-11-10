import { eq, and, gt, lt, gte, desc, asc, count } from "drizzle-orm";
import { useDb } from "./db";
import { authEvents } from "../database/drizzle/schema";

export interface AuthEvent {
  id: string;
  type: string;
  timestamp: Date;
  status: "success" | "failed";
  userId?: string;
  sessionId?: string;
  organizationId?: string;
  metadata?: Record<string, any>;
  ipAddress?: string;
  userAgent?: string;
  source: "app" | "api";
  display?: {
    message: string;
    severity?: "info" | "success" | "warning" | "failed";
  };
}

export interface EventQueryOptions {
  limit?: number;
  offset?: number;
  after?: string;
  sort?: "asc" | "desc";
  type?: string;
  userId?: string;
  since?: Date;
}

export interface EventQueryResult {
  events: AuthEvent[];
  hasMore: boolean;
  nextCursor: string | null;
}

export const createDrizzleEventsProvider = () => {
  const db = useDb();

  return {
    async ingest(event: AuthEvent): Promise<void> {
      try {
        await db.insert(authEvents).values({
          id: event.id || crypto.randomUUID(),
          type: event.type,
          timestamp: event.timestamp || new Date(),
          status: event.status || "success",
          userId: event.userId ?? null,
          sessionId: event.sessionId ?? null,
          organizationId: event.organizationId ?? null,
          metadata: event.metadata ? JSON.stringify(event.metadata) : null,
          ipAddress: event.ipAddress ?? null,
          userAgent: event.userAgent ?? null,
          source: event.source || "app",
          displayMessage: event.display?.message || event.type,
          displaySeverity: event.display?.severity || "info",
        });
      } catch (err) {
        console.error("[DrizzleEventsProvider] Ingest error:", err);
      }
    },

    async ingestBatch(events: AuthEvent[]): Promise<void> {
      for (const event of events) {
        await this.ingest(event);
      }
    },

    async query(options: EventQueryOptions = {}): Promise<EventQueryResult> {
      try {
        const limit = options.limit || 50;
        const conditions: any[] = [];

        if (options.userId) {
          conditions.push(eq(authEvents.userId, options.userId));
        }
        if (options.type) {
          conditions.push(eq(authEvents.type, options.type));
        }
        if (options.since) {
          conditions.push(gte(authEvents.timestamp, options.since));
        }
        if (options.after) {
          if (options.sort === "asc") {
            conditions.push(gt(authEvents.id, options.after));
          } else {
            conditions.push(lt(authEvents.id, options.after));
          }
        }

        const whereClause = conditions.length > 0 ? and(...conditions) : undefined;
        const orderBy = options.sort === "asc"
          ? [asc(authEvents.timestamp), asc(authEvents.id)]
          : [desc(authEvents.timestamp), desc(authEvents.id)];

        let query = db.select().from(authEvents);
        if (whereClause) {
          query = query.where(whereClause) as any;
        }
        query = query.orderBy(...orderBy).limit(limit + 1) as any;
        if (options.offset) {
          query = query.offset(options.offset) as any;
        }

        const rows = await query;
        const hasMore = rows.length > limit;
        const items = rows.slice(0, limit);

        const events: AuthEvent[] = items.map((row) => ({
          id: row.id,
          type: row.type,
          timestamp: new Date(row.timestamp),
          status: (row.status as any) || "success",
          userId: row.userId || undefined,
          sessionId: row.sessionId || undefined,
          organizationId: row.organizationId || undefined,
          metadata: row.metadata ? JSON.parse(row.metadata) : {},
          ipAddress: row.ipAddress || undefined,
          userAgent: row.userAgent || undefined,
          source: (row.source as any) || "app",
          display: {
            message: row.displayMessage || row.type,
            severity: (row.displaySeverity as any) || "info",
          },
        }));

        return {
          events,
          hasMore,
          nextCursor: hasMore && events.length > 0 ? events[events.length - 1].id : null,
        };
      } catch (err) {
        console.error("[DrizzleEventsProvider] Query error:", err);
        return { events: [], hasMore: false, nextCursor: null };
      }
    },

    async count(): Promise<number> {
      try {
        const res = await db.select({ val: count() }).from(authEvents);
        return Number(res[0]?.val ?? 0);
      } catch {
        return 0;
      }
    },

    async healthCheck(): Promise<boolean> {
      return true;
    },
  };
};
