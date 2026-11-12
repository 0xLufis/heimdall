-- Catches the migrations up with schema.ts: these objects were previously only ever created via
-- `drizzle-kit push`, so a database built from migrations alone lacked them. Idempotent on purpose,
-- so databases that already received them through push are not broken.
CREATE TABLE IF NOT EXISTS "auth"."auth_events" (
	"id" text PRIMARY KEY NOT NULL,
	"type" text NOT NULL,
	"timestamp" timestamp with time zone DEFAULT now() NOT NULL,
	"status" text DEFAULT 'success' NOT NULL,
	"user_id" text,
	"session_id" text,
	"organization_id" text,
	"metadata" text,
	"ip_address" text,
	"user_agent" text,
	"source" text DEFAULT 'app',
	"display_message" text,
	"display_severity" text,
	"created_at" timestamp with time zone DEFAULT now()
);
--> statement-breakpoint
ALTER TABLE "auth"."account" ADD COLUMN IF NOT EXISTS "issuer" text;--> statement-breakpoint
CREATE INDEX IF NOT EXISTS "idx_auth_events_user_id" ON "auth"."auth_events" USING btree ("user_id");--> statement-breakpoint
CREATE INDEX IF NOT EXISTS "idx_auth_events_type" ON "auth"."auth_events" USING btree ("type");--> statement-breakpoint
CREATE INDEX IF NOT EXISTS "idx_auth_events_timestamp" ON "auth"."auth_events" USING btree ("timestamp");