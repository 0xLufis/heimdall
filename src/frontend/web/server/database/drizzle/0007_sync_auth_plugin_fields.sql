-- Columns Better-Auth 1.7 plugins read and write that the auth schema lacked, plus Heimdall's
-- per-user permission grants. Idempotent on purpose (like 0006), so databases that already got a
-- column through `drizzle-kit push` or by hand are not broken.
--   invitation.created_at   organization plugin: inviting a member failed without it
--   session.impersonated_by admin plugin: impersonation failed without it
--   user.display_username   username plugin: sign-up with a username failed without it
--   user.permissions        explicit grants such as "organizations.manage" (comma-separated)
-- The admin plugin's `banExpires` field maps to the existing "ban_expires_at" column (schema.ts).
ALTER TABLE "auth"."invitation" ADD COLUMN IF NOT EXISTS "created_at" timestamp DEFAULT now() NOT NULL;--> statement-breakpoint
ALTER TABLE "auth"."session" ADD COLUMN IF NOT EXISTS "impersonated_by" text;--> statement-breakpoint
ALTER TABLE "auth"."user" ADD COLUMN IF NOT EXISTS "display_username" text;--> statement-breakpoint
ALTER TABLE "auth"."user" ADD COLUMN IF NOT EXISTS "permissions" text;
