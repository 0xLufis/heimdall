using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class OrthogonalEscalationAndPendingReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "escalated_at",
                schema: "backend",
                table: "maintenance_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalated_by",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "escalation_closed_at",
                schema: "backend",
                table: "maintenance_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_closed_by",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_reason",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_escalated",
                schema: "backend",
                table: "maintenance_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pending_details",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_reason",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "diagnostic_snapshots",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    machine_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    captured_by_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    captured_by_user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_payload_json = table.Column<string>(type: "text", nullable: false),
                    payload_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diagnostic_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_diagnostic_snapshots_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_groups",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    color = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    lead_engineer_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lead_engineer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    machine_ids_json = table.Column<string>(type: "text", nullable: false),
                    machine_types_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_absences",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    technician_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    marked_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    backup_technician_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    backup_technician_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shift_absences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "technician_rules",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    technician_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    technician_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    technician_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    scope_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    category_filter = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    backup_technician_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    backup_technician_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    assigned_by_role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_is_escalated",
                schema: "backend",
                table: "maintenance_tickets",
                column: "is_escalated");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_snapshots_client_pc_id_captured_at_utc",
                schema: "backend",
                table: "diagnostic_snapshots",
                columns: new[] { "client_pc_id", "captured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_snapshots_organization_id",
                schema: "backend",
                table: "diagnostic_snapshots",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnostic_snapshots",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_groups",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "shift_absences",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "technician_rules",
                schema: "backend");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_is_escalated",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalated_at",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalated_by",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalation_closed_at",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalation_closed_by",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "escalation_reason",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "is_escalated",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "pending_details",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "pending_reason",
                schema: "backend",
                table: "maintenance_tickets");
        }
    }
}
