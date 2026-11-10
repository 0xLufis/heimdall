using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InventorySegmentationAndParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "comment_id",
                schema: "backend",
                table: "ticket_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_id",
                schema: "backend",
                table: "stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "machine_type",
                schema: "backend",
                table: "stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_technician_id",
                schema: "backend",
                table: "stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_technician_name",
                schema: "backend",
                table: "stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "equipment_status",
                schema: "backend",
                table: "inventory_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_stock_item",
                schema: "backend",
                table: "inventory_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "machine_id",
                schema: "backend",
                table: "inventory_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_stock_threshold",
                schema: "backend",
                table: "inventory_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stock_quantity",
                schema: "backend",
                table: "inventory_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_location",
                schema: "backend",
                table: "inventory_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "technology",
                schema: "backend",
                table: "inventory_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ad_ou_path",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_profile_name",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_thumbprint",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "ou_tags",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subnet",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "vlan_id",
                schema: "backend",
                table: "client_pcs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vlan_name",
                schema: "backend",
                table: "client_pcs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ad_ou_path",
                schema: "backend",
                table: "client_certificates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_root_ca",
                schema: "backend",
                table: "client_certificates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "issuer",
                schema: "backend",
                table: "client_certificates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key_algorithm",
                schema: "backend",
                table: "client_certificates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_name",
                schema: "backend",
                table: "client_certificates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raw_pem",
                schema: "backend",
                table: "client_certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial_number",
                schema: "backend",
                table: "client_certificates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ad_ou_governances",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ou_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    access_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    approved_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ad_ou_governances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ou_certificate_rules",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ou_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    profile_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    validity_years = table.Column<int>(type: "integer", nullable: false),
                    auto_enroll = table.Column<bool>(type: "boolean", nullable: false),
                    key_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ou_certificate_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_machine_id",
                schema: "backend",
                table: "inventory_items",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_ou_certificate_rules_ou_path",
                schema: "backend",
                table: "ou_certificate_rules",
                column: "ou_path");

            migrationBuilder.AddForeignKey(
                name: "fk_inventory_items_stations_machine_id",
                schema: "backend",
                table: "inventory_items",
                column: "machine_id",
                principalSchema: "backend",
                principalTable: "stations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inventory_items_stations_machine_id",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropTable(
                name: "ad_ou_governances",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "ou_certificate_rules",
                schema: "backend");

            migrationBuilder.DropIndex(
                name: "ix_inventory_items_machine_id",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "comment_id",
                schema: "backend",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "machine_type",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "preferred_technician_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "preferred_technician_name",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "equipment_status",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "is_stock_item",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "machine_id",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "min_stock_threshold",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "stock_quantity",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "storage_location",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "technology",
                schema: "backend",
                table: "inventory_items");

            migrationBuilder.DropColumn(
                name: "ad_ou_path",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "certificate_profile_name",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "certificate_thumbprint",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "ou_tags",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "subnet",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "vlan_id",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "vlan_name",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "ad_ou_path",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "is_root_ca",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "issuer",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "key_algorithm",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "profile_name",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "raw_pem",
                schema: "backend",
                table: "client_certificates");

            migrationBuilder.DropColumn(
                name: "serial_number",
                schema: "backend",
                table: "client_certificates");
        }
    }
}
