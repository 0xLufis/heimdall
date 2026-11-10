using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "backend",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "backend",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "lead_time_days",
                schema: "backend",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "backend",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                schema: "backend",
                table: "suppliers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "author",
                schema: "backend",
                table: "software_assets",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checksum_sha256",
                schema: "backend",
                table: "software_assets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "software_type",
                schema: "backend",
                table: "software_assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "specific_data_json",
                schema: "backend",
                table: "software_assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "backend",
                table: "software_assets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "affected_component_ids_json",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alarm_code",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "controller_source",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plc_error_id",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_component_bmk",
                schema: "backend",
                table: "maintenance_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_component_id",
                schema: "backend",
                table: "maintenance_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telemetry_probes_json",
                schema: "backend",
                table: "maintenance_tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_type",
                schema: "backend",
                table: "machine_groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "interface_contract",
                schema: "backend",
                table: "machine_groups",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "technology_key",
                schema: "backend",
                table: "machine_groups",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "component_recipes",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    extends_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    model_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    base_cost_eur = table.Column<decimal>(type: "numeric", nullable: true),
                    specification_schema_json = table.Column<string>(type: "text", nullable: true),
                    auto_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    eplan_cad_defaults_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_recipes", x => x.id);
                    table.ForeignKey(
                        name: "fk_component_recipes_component_recipes_extends_recipe_id",
                        column: x => x.extends_recipe_id,
                        principalSchema: "backend",
                        principalTable: "component_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "functional_settings",
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
                    table.PrimaryKey("pk_functional_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "known_tags",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_known_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "license_assets",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    license_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    license_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    vendor = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    seats_count = table.Column<int>(type: "integer", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_perpetual = table.Column<bool>(type: "boolean", nullable: false),
                    hardware_lock_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_license_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_license_assets_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_documents",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    revision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    storage_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_machine_documents_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_group_memberships",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_group_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_machine_group_memberships_machine_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "backend",
                        principalTable: "machine_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_machine_group_memberships_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_service_logs",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entry_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    summary = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    linked_ticket_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_service_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_machine_service_logs_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_groups",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    discipline = table.Column<string>(type: "text", nullable: false),
                    shift_pattern = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_groups",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    parent_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permissions_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_groups_security_groups_parent_group_id",
                        column: x => x.parent_group_id,
                        principalSchema: "backend",
                        principalTable: "security_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skill_definitions",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    validity_months = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machine_components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    functional_slot_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    component_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_part_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    attributes_json = table.Column<string>(type: "text", nullable: true),
                    eplan_cad_ref_json = table.Column<string>(type: "text", nullable: true),
                    installed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_machine_components_component_recipes_component_recipe_id",
                        column: x => x.component_recipe_id,
                        principalSchema: "backend",
                        principalTable: "component_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_machine_components_inventory_items_current_part_id",
                        column: x => x.current_part_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_machine_components_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_item_tags",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tag_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_item_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_item_tags_known_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "backend",
                        principalTable: "known_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delegation_recipes",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    discipline = table.Column<string>(type: "text", nullable: false),
                    trigger_event_category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    allowed_schedules = table.Column<int>(type: "integer", nullable: false),
                    required_skill_ids_json = table.Column<string>(type: "text", nullable: false),
                    required_skill_codes_json = table.Column<string>(type: "text", nullable: false),
                    primary_personnel_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    primary_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    primary_user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    on_call_fallback_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    on_call_fallback_user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    escalation_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escalation_timeout_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegation_recipes", x => x.id);
                    table.ForeignKey(
                        name: "fk_delegation_recipes_personnel_groups_escalation_group_id",
                        column: x => x.escalation_group_id,
                        principalSchema: "backend",
                        principalTable: "personnel_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_delegation_recipes_personnel_groups_primary_personnel_group",
                        column: x => x.primary_personnel_group_id,
                        principalSchema: "backend",
                        principalTable: "personnel_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "personnel_group_members",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personnel_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_group_members_personnel_groups_personnel_group_id",
                        column: x => x.personnel_group_id,
                        principalSchema: "backend",
                        principalTable: "personnel_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_group_members",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_group_members_security_groups_security_group_id",
                        column: x => x.security_group_id,
                        principalSchema: "backend",
                        principalTable: "security_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personnel_skills",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    certificate_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    verified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personnel_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_personnel_skills_skill_definitions_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "backend",
                        principalTable: "skill_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "component_part_histories",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    swapped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    swapped_by_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    swapped_by_user_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    previous_part_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_part_serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    new_part_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_part_serial_number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    previous_part_disposition = table.Column<string>(type: "text", nullable: false),
                    associated_ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    previous_state_snapshot_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_part_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_component_part_histories_machine_components_component_id",
                        column: x => x.component_id,
                        principalSchema: "backend",
                        principalTable: "machine_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_delegation_bindings",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegation_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    bound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machine_delegation_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_machine_delegation_bindings_delegation_recipes_delegation_r",
                        column: x => x.delegation_recipe_id,
                        principalSchema: "backend",
                        principalTable: "delegation_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_machine_delegation_bindings_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_target_component_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "target_component_id");

            migrationBuilder.CreateIndex(
                name: "ix_component_part_histories_component_id",
                schema: "backend",
                table: "component_part_histories",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "ix_component_part_histories_machine_id",
                schema: "backend",
                table: "component_part_histories",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_component_part_histories_swapped_at",
                schema: "backend",
                table: "component_part_histories",
                column: "swapped_at");

            migrationBuilder.CreateIndex(
                name: "ix_component_recipes_extends_recipe_id",
                schema: "backend",
                table: "component_recipes",
                column: "extends_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_component_recipes_recipe_code",
                schema: "backend",
                table: "component_recipes",
                column: "recipe_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delegation_recipes_escalation_group_id",
                schema: "backend",
                table: "delegation_recipes",
                column: "escalation_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_delegation_recipes_primary_personnel_group_id",
                schema: "backend",
                table: "delegation_recipes",
                column: "primary_personnel_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_item_tags_tag_id",
                schema: "backend",
                table: "inventory_item_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_license_assets_machine_id",
                schema: "backend",
                table: "license_assets",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_components_component_recipe_id",
                schema: "backend",
                table: "machine_components",
                column: "component_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_components_current_part_id",
                schema: "backend",
                table: "machine_components",
                column: "current_part_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_components_machine_id_functional_slot_identifier",
                schema: "backend",
                table: "machine_components",
                columns: new[] { "machine_id", "functional_slot_identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_machine_delegation_bindings_delegation_recipe_id",
                schema: "backend",
                table: "machine_delegation_bindings",
                column: "delegation_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_delegation_bindings_machine_id_delegation_recipe_id",
                schema: "backend",
                table: "machine_delegation_bindings",
                columns: new[] { "machine_id", "delegation_recipe_id", "discipline" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_machine_documents_machine_id",
                schema: "backend",
                table: "machine_documents",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_group_memberships_group_id_machine_id",
                schema: "backend",
                table: "machine_group_memberships",
                columns: new[] { "group_id", "machine_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_machine_group_memberships_machine_id",
                schema: "backend",
                table: "machine_group_memberships",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_service_logs_machine_id",
                schema: "backend",
                table: "machine_service_logs",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_service_logs_timestamp",
                schema: "backend",
                table: "machine_service_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_group_members_personnel_group_id_user_id",
                schema: "backend",
                table: "personnel_group_members",
                columns: new[] { "personnel_group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_personnel_skills_skill_id",
                schema: "backend",
                table: "personnel_skills",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_personnel_skills_user_id_skill_id",
                schema: "backend",
                table: "personnel_skills",
                columns: new[] { "user_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_group_members_security_group_id_user_id",
                schema: "backend",
                table: "security_group_members",
                columns: new[] { "security_group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_groups_parent_group_id",
                schema: "backend",
                table: "security_groups",
                column: "parent_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_definitions_code",
                schema: "backend",
                table: "skill_definitions",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_maintenance_tickets_machine_components_target_component_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "target_component_id",
                principalSchema: "backend",
                principalTable: "machine_components",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_maintenance_tickets_machine_components_target_component_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropTable(
                name: "component_part_histories",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "functional_settings",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "inventory_item_tags",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "license_assets",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_delegation_bindings",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_documents",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_group_memberships",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_service_logs",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "personnel_group_members",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "personnel_skills",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "security_group_members",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "machine_components",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "known_tags",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "delegation_recipes",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "skill_definitions",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "security_groups",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "component_recipes",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "personnel_groups",
                schema: "backend");

            migrationBuilder.DropIndex(
                name: "ix_maintenance_tickets_target_component_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "backend",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "backend",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "lead_time_days",
                schema: "backend",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "backend",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "phone_number",
                schema: "backend",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "author",
                schema: "backend",
                table: "software_assets");

            migrationBuilder.DropColumn(
                name: "checksum_sha256",
                schema: "backend",
                table: "software_assets");

            migrationBuilder.DropColumn(
                name: "software_type",
                schema: "backend",
                table: "software_assets");

            migrationBuilder.DropColumn(
                name: "specific_data_json",
                schema: "backend",
                table: "software_assets");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "backend",
                table: "software_assets");

            migrationBuilder.DropColumn(
                name: "affected_component_ids_json",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "alarm_code",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "controller_source",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "plc_error_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "target_component_bmk",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "target_component_id",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "telemetry_probes_json",
                schema: "backend",
                table: "maintenance_tickets");

            migrationBuilder.DropColumn(
                name: "group_type",
                schema: "backend",
                table: "machine_groups");

            migrationBuilder.DropColumn(
                name: "interface_contract",
                schema: "backend",
                table: "machine_groups");

            migrationBuilder.DropColumn(
                name: "technology_key",
                schema: "backend",
                table: "machine_groups");
        }
    }
}
