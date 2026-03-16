using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "floor_plan_id",
                table: "client_pcs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "machine_id",
                table: "client_pcs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pinned_object_handle",
                table: "client_pcs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "floor_plans",
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
                name: "hardware_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    retailer_code = table.Column<string>(type: "text", nullable: true),
                    cost_in_huf = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_components", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    hw_components = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    sw_components = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_machines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "software_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    purchase_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    serial_number = table.Column<string>(type: "text", nullable: true),
                    retailer_code = table.Column<string>(type: "text", nullable: true),
                    cost_in_huf = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_software_components", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    privileges = table.Column<List<string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_floor_plan_id",
                table: "client_pcs",
                column: "floor_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_machine_id",
                table: "client_pcs",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_pinned_object_handle",
                table: "client_pcs",
                column: "pinned_object_handle");

            migrationBuilder.CreateIndex(
                name: "ix_floor_plans_name",
                table: "floor_plans",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_machines_custom_identifier",
                table: "machines",
                column: "custom_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_name",
                table: "user_roles",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_client_pcs_machines_machine_id",
                table: "client_pcs",
                column: "machine_id",
                principalTable: "machines",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_client_pcs_machines_machine_id",
                table: "client_pcs");

            migrationBuilder.DropTable(
                name: "floor_plans");

            migrationBuilder.DropTable(
                name: "hardware_components");

            migrationBuilder.DropTable(
                name: "machines");

            migrationBuilder.DropTable(
                name: "software_components");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_floor_plan_id",
                table: "client_pcs");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_machine_id",
                table: "client_pcs");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_pinned_object_handle",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "floor_plan_id",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "pinned_object_handle",
                table: "client_pcs");
        }
    }
}
