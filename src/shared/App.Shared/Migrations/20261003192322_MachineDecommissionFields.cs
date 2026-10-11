using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MachineDecommissionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE backend.stations ADD COLUMN IF NOT EXISTS decommission_date timestamp with time zone;
                ALTER TABLE backend.stations ADD COLUMN IF NOT EXISTS decommission_disposition character varying(64);
                ALTER TABLE backend.stations ADD COLUMN IF NOT EXISTS decommission_reason text;
                ALTER TABLE backend.stations ADD COLUMN IF NOT EXISTS status character varying(64) NOT NULL DEFAULT 'active';
            ");

            migrationBuilder.AddColumn<string>(
                name: "change_history",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_handover_state",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "escalation_target",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_operator_id",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_operator_name",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_line_stop",
                schema: "backend",
                table: "maintenance_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "issue_type",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "line_stop_duration_minutes",
                schema: "backend",
                table: "maintenance_tickets",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "originator_type",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "qr_scanned_at",
                schema: "backend",
                table: "maintenance_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "reaction_time_minutes",
                schema: "backend",
                table: "maintenance_tickets",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reserved_by",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "responsible_department",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "backend",
                table: "maintenance_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tags",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telemetry_snapshot",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_is_line_stop",
                schema: "backend",
                table: "maintenance_tickets",
                column: "is_line_stop");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_responsible_department",
                schema: "backend",
                table: "maintenance_tickets",
                column: "responsible_department");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_is_line_stop",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_responsible_department",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "decommission_date",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "decommission_disposition",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "decommission_reason",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "change_history",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalation_handover_state",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalation_target",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "external_operator_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "external_operator_name",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "is_line_stop",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "issue_type",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "line_stop_duration_minutes",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "originator_type",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "qr_scanned_at",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "reaction_time_minutes",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "reserved_by",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "responsible_department",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "telemetry_snapshot",
                schema: "backend",
                table: "maintenance_tickets");
        }
    }
}
