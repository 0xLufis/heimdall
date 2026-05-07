using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "backend");

            migrationBuilder.CreateTable(
                name: "client_pcs",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mac_address = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    machine_identifier = table.Column<string>(type: "text", nullable: true),
                    hostname = table.Column<string>(type: "text", nullable: true),
                    last_online = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pinned_object_handle = table.Column<string>(type: "text", nullable: true),
                    free_disk_space = table.Column<DiskSpaceInfo>(type: "jsonb", nullable: true),
                    monitoring_config = table.Column<ResourceMonitoringConfig>(type: "jsonb", nullable: true),
                    resource_averages = table.Column<ResourceAverages>(type: "jsonb", nullable: true),
                    alerting_limits = table.Column<AlertingLimits>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_pcs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "floor_plan_anchor",
                schema: "backend",
                columns: table => new
                {
                    handle = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: true),
                    y = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "floor_plans",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    svg_content = table.Column<string>(type: "text", nullable: false),
                    anchors = table.Column<List<FloorPlanAnchor>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manufacturers",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    website = table.Column<string>(type: "text", nullable: true),
                    support_contact = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manufacturers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "responsible_teams",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_responsible_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    website = table.Column<string>(type: "text", nullable: true),
                    contact_person = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    privileges = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_events",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_events_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queued_agent_commands",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    signature = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queued_agent_commands", x => x.id);
                    table.ForeignKey(
                        name: "fk_queued_agent_commands_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcResponsibilities",
                schema: "backend",
                columns: table => new
                {
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_teams_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pc_responsibilities", x => new { x.client_pc_id, x.responsible_teams_id });
                    table.ForeignKey(
                        name: "fk_pc_responsibilities_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pc_responsibilities_responsible_teams_responsible_teams_id",
                        column: x => x.responsible_teams_id,
                        principalSchema: "backend",
                        principalTable: "responsible_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    organization_id = table.Column<string>(type: "text", nullable: true),
                    cost_in_huf = table.Column<decimal>(type: "numeric", nullable: true),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    manufacturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_items_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_inventory_items_inventory_items_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_items_manufacturers_manufacturer_id",
                        column: x => x.manufacturer_id,
                        principalSchema: "backend",
                        principalTable: "manufacturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_items_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "backend",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hardware_assets",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<string>(type: "text", nullable: true),
                    model_number = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hardware_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_hardware_assets_inventory_items_id",
                        column: x => x.id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemResponsibilities",
                schema: "backend",
                columns: table => new
                {
                    managed_items_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_teams_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_responsibilities", x => new { x.managed_items_id, x.responsible_teams_id });
                    table.ForeignKey(
                        name: "fk_item_responsibilities_inventory_items_managed_items_id",
                        column: x => x.managed_items_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_item_responsibilities_responsible_teams_responsible_teams_id",
                        column: x => x.responsible_teams_id,
                        principalSchema: "backend",
                        principalTable: "responsible_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pc_hardware",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pc_hardware", x => x.id);
                    table.ForeignKey(
                        name: "fk_pc_hardware_inventory_items_id",
                        column: x => x.id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stations",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pinned_object_handle = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.id);
                    table.ForeignKey(
                        name: "fk_stations_inventory_items_id",
                        column: x => x.id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "software_assets",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "text", nullable: true),
                    license_key = table.Column<string>(type: "text", nullable: true),
                    hardware_component_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_software_assets_hardware_assets_hardware_component_id",
                        column: x => x.hardware_component_id,
                        principalSchema: "backend",
                        principalTable: "hardware_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_software_assets_inventory_items_id",
                        column: x => x.id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StationControllers",
                schema: "backend",
                columns: table => new
                {
                    controlled_machines_id = table.Column<Guid>(type: "uuid", nullable: false),
                    controllers_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_station_controllers", x => new { x.controlled_machines_id, x.controllers_id });
                    table.ForeignKey(
                        name: "fk_station_controllers_client_pcs_controllers_id",
                        column: x => x.controllers_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_station_controllers_stations_controlled_machines_id",
                        column: x => x.controlled_machines_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_events_client_pc_id",
                schema: "backend",
                table: "agent_events",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_mac_address",
                schema: "backend",
                table: "client_pcs",
                column: "mac_address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_floor_plans_name",
                schema: "backend",
                table: "floor_plans",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_client_pc_id",
                schema: "backend",
                table: "inventory_items",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_manufacturer_id",
                schema: "backend",
                table: "inventory_items",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_parent_id",
                schema: "backend",
                table: "inventory_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_supplier_id",
                schema: "backend",
                table: "inventory_items",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_responsibilities_responsible_teams_id",
                schema: "backend",
                table: "ItemResponsibilities",
                column: "responsible_teams_id");

            migrationBuilder.CreateIndex(
                name: "ix_manufacturers_name",
                schema: "backend",
                table: "manufacturers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pc_responsibilities_responsible_teams_id",
                schema: "backend",
                table: "PcResponsibilities",
                column: "responsible_teams_id");

            migrationBuilder.CreateIndex(
                name: "ix_queued_agent_commands_client_pc_id",
                schema: "backend",
                table: "queued_agent_commands",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_responsible_teams_name",
                schema: "backend",
                table: "responsible_teams",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_software_assets_hardware_component_id",
                schema: "backend",
                table: "software_assets",
                column: "hardware_component_id");

            migrationBuilder.CreateIndex(
                name: "ix_station_controllers_controllers_id",
                schema: "backend",
                table: "StationControllers",
                column: "controllers_id");

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name",
                schema: "backend",
                table: "suppliers",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_events",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "floor_plan_anchor",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "floor_plans",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "ItemResponsibilities",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "pc_hardware",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "PcResponsibilities",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "queued_agent_commands",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "software_assets",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "StationControllers",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "responsible_teams",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "hardware_assets",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "stations",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "client_pcs",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "manufacturers",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "backend");
        }
    }
}
