-- "auth" always exists by the time this runs (infra/database/init creates it and drizzle-kit keeps
-- its own migrations table there), so a plain schema rename fails with 42P06 on a fresh database.
-- Move the tables instead; the end state is identical.
CREATE SCHEMA IF NOT EXISTS "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."account" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."invitation" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."member" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."organization" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."session" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."user" SET SCHEMA "auth";
--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."verification" SET SCHEMA "auth";
--> statement-breakpoint
DROP SCHEMA "heimdall_dev_db";
--> statement-breakpoint
ALTER TABLE "auth"."account" DROP CONSTRAINT "account_user_id_user_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."invitation" DROP CONSTRAINT "invitation_organization_id_organization_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."invitation" DROP CONSTRAINT "invitation_inviter_id_user_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."member" DROP CONSTRAINT "member_organization_id_organization_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."member" DROP CONSTRAINT "member_user_id_user_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."session" DROP CONSTRAINT "session_user_id_user_id_fk";
--> statement-breakpoint
ALTER TABLE "auth"."account" ADD CONSTRAINT "account_user_id_user_id_fk" FOREIGN KEY ("user_id") REFERENCES "auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "auth"."invitation" ADD CONSTRAINT "invitation_organization_id_organization_id_fk" FOREIGN KEY ("organization_id") REFERENCES "auth"."organization"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "auth"."invitation" ADD CONSTRAINT "invitation_inviter_id_user_id_fk" FOREIGN KEY ("inviter_id") REFERENCES "auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "auth"."member" ADD CONSTRAINT "member_organization_id_organization_id_fk" FOREIGN KEY ("organization_id") REFERENCES "auth"."organization"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "auth"."member" ADD CONSTRAINT "member_user_id_user_id_fk" FOREIGN KEY ("user_id") REFERENCES "auth"."user"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "auth"."session" ADD CONSTRAINT "session_user_id_user_id_fk" FOREIGN KEY ("user_id") REFERENCES "auth"."user"("id") ON DELETE cascade ON UPDATE no action;