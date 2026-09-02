using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class SystemGovernanceAndPki : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_events_client_pc_id",
                schema: "backend",
                table: "agent_events");

            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "queued_agent_commands",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "agent_events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    old_values_json = table.Column<string>(type: "text", nullable: true),
                    new_values_json = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_certificates",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    common_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_certificates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "malformed_telemetry_quarantine",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ingestion_channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    error_reason = table.Column<string>(type: "text", nullable: false),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    quarantined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_malformed_telemetry_quarantine", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schema_version_manifest",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    migration_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schema_version_manifest", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_group_mappings",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    group_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mapped_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_group_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "backend",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_json = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_queued_agent_commands_organization_id",
                schema: "backend",
                table: "queued_agent_commands",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_assigned_to",
                schema: "backend",
                table: "maintenance_tickets",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_created_at",
                schema: "backend",
                table: "maintenance_tickets",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_organization_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_priority",
                schema: "backend",
                table: "maintenance_tickets",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_status",
                schema: "backend",
                table: "maintenance_tickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_organization_id",
                schema: "backend",
                table: "client_pcs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_events_client_pc_id_timestamp",
                schema: "backend",
                table: "agent_events",
                columns: new[] { "client_pc_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_events_level",
                schema: "backend",
                table: "agent_events",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "ix_agent_events_organization_id",
                schema: "backend",
                table: "agent_events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type",
                schema: "backend",
                table: "audit_logs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_id",
                schema: "backend",
                table: "audit_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_timestamp",
                schema: "backend",
                table: "audit_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id",
                schema: "backend",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_certificates_client_pc_id",
                schema: "backend",
                table: "client_certificates",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_certificates_common_name",
                schema: "backend",
                table: "client_certificates",
                column: "common_name");

            migrationBuilder.CreateIndex(
                name: "ix_client_certificates_thumbprint",
                schema: "backend",
                table: "client_certificates",
                column: "thumbprint");

            migrationBuilder.CreateIndex(
                name: "ix_malformed_telemetry_quarantine_ingestion_channel",
                schema: "backend",
                table: "malformed_telemetry_quarantine",
                column: "ingestion_channel");

            migrationBuilder.CreateIndex(
                name: "ix_malformed_telemetry_quarantine_organization_id",
                schema: "backend",
                table: "malformed_telemetry_quarantine",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_malformed_telemetry_quarantine_quarantined_at",
                schema: "backend",
                table: "malformed_telemetry_quarantine",
                column: "quarantined_at");

            migrationBuilder.CreateIndex(
                name: "ix_schema_version_manifest_schema_version",
                schema: "backend",
                table: "schema_version_manifest",
                column: "schema_version");

            migrationBuilder.CreateIndex(
                name: "ix_security_group_mappings_group_identifier",
                schema: "backend",
                table: "security_group_mappings",
                column: "group_identifier");

            migrationBuilder.CreateIndex(
                name: "ix_security_group_mappings_organization_id",
                schema: "backend",
                table: "security_group_mappings",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_category",
                schema: "backend",
                table: "system_settings",
                column: "category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "client_certificates",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "malformed_telemetry_quarantine",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "schema_version_manifest",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "security_group_mappings",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "backend");

            migrationBuilder.DropIndex(
                name: "ix_queued_agent_commands_organization_id",
                schema: "backend",
                table: "queued_agent_commands");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_assigned_to",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_created_at",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_organization_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_priority",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_status",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_organization_id",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropIndex(
                name: "ix_agent_events_client_pc_id_timestamp",
                schema: "backend",
                table: "agent_events");

            migrationBuilder.DropIndex(
                name: "ix_agent_events_level",
                schema: "backend",
                table: "agent_events");

            migrationBuilder.DropIndex(
                name: "ix_agent_events_organization_id",
                schema: "backend",
                table: "agent_events");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "queued_agent_commands");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "agent_events");

            migrationBuilder.CreateIndex(
                name: "ix_agent_events_client_pc_id",
                schema: "backend",
                table: "agent_events",
                column: "client_pc_id");
        }
    }
}
