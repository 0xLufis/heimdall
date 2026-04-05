using System;
using System.Text.Json;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToUnifiedInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "components",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "hardware_components",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "software_components",
                schema: "backend");

            migrationBuilder.DropColumn(
                name: "hw_components",
                schema: "backend",
                table: "machines");

            migrationBuilder.DropColumn(
                name: "sw_components",
                schema: "backend",
                table: "machines");

            migrationBuilder.DropColumn(
                name: "hardware_config",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "software_config",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.CreateTable(
                name: "inventory_components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    technology = table.Column<string>(type: "text", nullable: true),
                    top_level_flags = table.Column<ComponentTopLevelFlags>(type: "jsonb", nullable: true),
                    data = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    manufacturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lateral_link_id = table.Column<Guid>(type: "uuid", nullable: true),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_pc_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_components_client_pcs_client_pc_id",
                        column: x => x.client_pc_id,
                        principalSchema: "backend",
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_components_inventory_components_lateral_link_id",
                        column: x => x.lateral_link_id,
                        principalSchema: "backend",
                        principalTable: "inventory_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_inventory_components_inventory_components_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "backend",
                        principalTable: "inventory_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_components_machines_machine_id",
                        column: x => x.machine_id,
                        principalSchema: "backend",
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_components_manufacturers_manufacturer_id",
                        column: x => x.manufacturer_id,
                        principalSchema: "backend",
                        principalTable: "manufacturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_components_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "backend",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_client_pc_id",
                schema: "backend",
                table: "inventory_components",
                column: "client_pc_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_lateral_link_id",
                schema: "backend",
                table: "inventory_components",
                column: "lateral_link_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_machine_id",
                schema: "backend",
                table: "inventory_components",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_manufacturer_id",
                schema: "backend",
                table: "inventory_components",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_parent_id",
                schema: "backend",
                table: "inventory_components",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_components_supplier_id",
                schema: "backend",
                table: "inventory_components",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_components",
                schema: "backend");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "hw_components",
                schema: "backend",
                table: "machines",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "sw_components",
                schema: "backend",
                table: "machines",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<HardwareConfig>(
                name: "hardware_config",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<SoftwareConfig>(
                name: "software_config",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_managed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hardware_components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_in_huf = table.Column<decimal>(type: "numeric", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    model_number = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<string>(type: "text", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    technical_specs = table.Column<ComponentTechnicalSpecs>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_hardware_components_hardware_components_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "backend",
                        principalTable: "hardware_components",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_hardware_components_manufacturers_manufacturer_id",
                        column: x => x.manufacturer_id,
                        principalSchema: "backend",
                        principalTable: "manufacturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hardware_components_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "backend",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "software_components",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_in_huf = table.Column<decimal>(type: "numeric", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_software_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_software_components_manufacturers_manufacturer_id",
                        column: x => x.manufacturer_id,
                        principalSchema: "backend",
                        principalTable: "manufacturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_software_components_software_components_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "backend",
                        principalTable: "software_components",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_software_components_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalSchema: "backend",
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_manufacturer_id",
                schema: "backend",
                table: "hardware_components",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_parent_id",
                schema: "backend",
                table: "hardware_components",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_supplier_id",
                schema: "backend",
                table: "hardware_components",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_software_components_manufacturer_id",
                schema: "backend",
                table: "software_components",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_software_components_parent_id",
                schema: "backend",
                table: "software_components",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_software_components_supplier_id",
                schema: "backend",
                table: "software_components",
                column: "supplier_id");
        }
    }
}
