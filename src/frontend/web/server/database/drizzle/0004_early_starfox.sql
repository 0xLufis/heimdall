ALTER TABLE "heimdall_dev_db"."user" ADD COLUMN "banned" boolean;--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."user" ADD COLUMN "ban_reason" text;--> statement-breakpoint
ALTER TABLE "heimdall_dev_db"."user" ADD COLUMN "ban_expires_at" timestamp;