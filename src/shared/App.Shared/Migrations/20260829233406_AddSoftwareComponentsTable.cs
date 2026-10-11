using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftwareComponentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_station_controllers_client_pcs_controllers_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropForeignKey(
                name: "fk_station_controllers_stations_controlled_machines_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropPrimaryKey(
                name: "pk_station_controllers",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropIndex(
                name: "ix_station_controllers_controllers_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.RenameColumn(
                name: "controllers_id",
                schema: "backend",
                table: "StationControllers",
                newName: "machine_id");

            migrationBuilder.RenameColumn(
                name: "controlled_machines_id",
                schema: "backend",
                table: "StationControllers",
                newName: "client_pc_id");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                schema: "backend",
                table: "StationControllers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                schema: "backend",
                table: "StationControllers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<JsonDocument>(
                name: "metadata",
                schema: "backend",
                table: "StationControllers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "backend",
                table: "StationControllers",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_station_controllers",
                schema: "backend",
                table: "StationControllers",
                column: "id");

            migrationBuilder.CreateTable(
                name: "equipment_interconnects",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interconnect_type = table.Column<string>(type: "text", nullable: false),
                    connection_string = table.Column<string>(type: "text", nullable: true),
                    port_or_address = table.Column<string>(type: "text", nullable: true),
                    protocol = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_interconnects", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_interconnects_inventory_items_source_equipment_id",
                        column: x => x.source_equipment_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_interconnects_inventory_items_target_equipment_id",
                        column: x => x.target_equipment_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_tickets",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_inventory_items_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "backend",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_maintenance_tickets_stations_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "software_components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_software_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_software_components_software_assets_id",
                        column: x => x.id,
                        principalSchema: "backend",
                        principalTable: "software_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_attachments",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    maintenance_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_attachments_maintenance_tickets_maintenance_ticket_id",
                        column: x => x.maintenance_ticket_id,
                        principalSchema: "backend",
                        principalTable: "maintenance_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comments",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    maintenance_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_comments_maintenance_tickets_maintenance_ticket_id",
                        column: x => x.maintenance_ticket_id,
                        principalSchema: "backend",
                        principalTable: "maintenance_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_station_controllers_client_pc_id",
                schema: "backend",
                table: "StationControllers",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_station_controllers_machine_id_client_pc_id",
                schema: "backend",
                table: "StationControllers",
                columns: new[] { "machine_id", "client_pc_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_station_controllers_metadata",
                schema: "backend",
                table: "StationControllers",
                column: "metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_metadata",
                schema: "backend",
                table: "inventory_items",
                column: "metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_system_metadata",
                schema: "backend",
                table: "client_pcs",
                column: "system_metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_interconnects_metadata",
                schema: "backend",
                table: "equipment_interconnects",
                column: "metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_interconnects_source_equipment_id",
                schema: "backend",
                table: "equipment_interconnects",
                column: "source_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_interconnects_target_equipment_id",
                schema: "backend",
                table: "equipment_interconnects",
                column: "target_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_client_pc_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_equipment_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_machine_id",
                schema: "backend",
                table: "maintenance_tickets",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_tickets_metadata",
                schema: "backend",
                table: "maintenance_tickets",
                column: "metadata")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_maintenance_ticket_id",
                schema: "backend",
                table: "ticket_attachments",
                column: "maintenance_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comments_maintenance_ticket_id",
                schema: "backend",
                table: "ticket_comments",
                column: "maintenance_ticket_id");

            migrationBuilder.AddForeignKey(
                name: "fk_station_controllers_client_pcs_client_pc_id",
                schema: "backend",
                table: "StationControllers",
                column: "client_pc_id",
                principalSchema: "backend",
                principalTable: "client_pcs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_station_controllers_stations_machine_id",
                schema: "backend",
                table: "StationControllers",
                column: "machine_id",
                principalSchema: "backend",
                principalTable: "stations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_station_controllers_client_pcs_client_pc_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropForeignKey(
                name: "fk_station_controllers_stations_machine_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropTable(
                name: "equipment_interconnects",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "software_components",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "ticket_attachments",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "ticket_comments",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "maintenance_tickets",
                schema: "backend");

            migrationBuilder.DropPrimaryKey(
                name: "pk_station_controllers",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropIndex(
                name: "ix_station_controllers_client_pc_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropIndex(
                name: "ix_station_controllers_machine_id_client_pc_id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropIndex(
                name: "ix_station_controllers_metadata",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropIndex(
                name: "ix_inventory_items_metadata",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_system_metadata",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropColumn(
                name: "metadata",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "backend",
                table: "StationControllers");

            migrationBuilder.RenameColumn(
                name: "machine_id",
                schema: "backend",
                table: "StationControllers",
                newName: "controllers_id");

            migrationBuilder.RenameColumn(
                name: "client_pc_id",
                schema: "backend",
                table: "StationControllers",
                newName: "controlled_machines_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_station_controllers",
                schema: "backend",
                table: "StationControllers",
                columns: new[] { "controlled_machines_id", "controllers_id" });

            migrationBuilder.CreateIndex(
                name: "ix_station_controllers_controllers_id",
                schema: "backend",
                table: "StationControllers",
                column: "controllers_id");

            migrationBuilder.AddForeignKey(
                name: "fk_station_controllers_client_pcs_controllers_id",
                schema: "backend",
                table: "StationControllers",
                column: "controllers_id",
                principalSchema: "backend",
                principalTable: "client_pcs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_station_controllers_stations_controlled_machines_id",
                schema: "backend",
                table: "StationControllers",
                column: "controlled_machines_id",
                principalSchema: "backend",
                principalTable: "stations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
