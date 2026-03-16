using System;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RefactorComponentsAndSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "manufacturer",
                table: "software_components");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                table: "hardware_components");

            migrationBuilder.RenameColumn(
                name: "retailer_code",
                table: "software_components",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "retailer_code",
                table: "hardware_components",
                newName: "revision");

            migrationBuilder.AddColumn<Guid>(
                name: "manufacturer_id",
                table: "software_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "software_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manufacturer_id",
                table: "hardware_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_number",
                table: "hardware_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                table: "hardware_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<ComponentTechnicalSpecs>(
                name: "technical_specs",
                table: "hardware_components",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "manufacturers",
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
                name: "suppliers",
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

            migrationBuilder.CreateIndex(
                name: "ix_software_components_manufacturer_id",
                table: "software_components",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_software_components_supplier_id",
                table: "software_components",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_manufacturer_id",
                table: "hardware_components",
                column: "manufacturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_supplier_id",
                table: "hardware_components",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_manufacturers_name",
                table: "manufacturers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name",
                table: "suppliers",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_hardware_components_manufacturers_manufacturer_id",
                table: "hardware_components",
                column: "manufacturer_id",
                principalTable: "manufacturers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_hardware_components_suppliers_supplier_id",
                table: "hardware_components",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_software_components_manufacturers_manufacturer_id",
                table: "software_components",
                column: "manufacturer_id",
                principalTable: "manufacturers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_software_components_suppliers_supplier_id",
                table: "software_components",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hardware_components_manufacturers_manufacturer_id",
                table: "hardware_components");

            migrationBuilder.DropForeignKey(
                name: "fk_hardware_components_suppliers_supplier_id",
                table: "hardware_components");

            migrationBuilder.DropForeignKey(
                name: "fk_software_components_manufacturers_manufacturer_id",
                table: "software_components");

            migrationBuilder.DropForeignKey(
                name: "fk_software_components_suppliers_supplier_id",
                table: "software_components");

            migrationBuilder.DropTable(
                name: "manufacturers");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropIndex(
                name: "ix_software_components_manufacturer_id",
                table: "software_components");

            migrationBuilder.DropIndex(
                name: "ix_software_components_supplier_id",
                table: "software_components");

            migrationBuilder.DropIndex(
                name: "ix_hardware_components_manufacturer_id",
                table: "hardware_components");

            migrationBuilder.DropIndex(
                name: "ix_hardware_components_supplier_id",
                table: "hardware_components");

            migrationBuilder.DropColumn(
                name: "manufacturer_id",
                table: "software_components");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "software_components");

            migrationBuilder.DropColumn(
                name: "manufacturer_id",
                table: "hardware_components");

            migrationBuilder.DropColumn(
                name: "model_number",
                table: "hardware_components");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                table: "hardware_components");

            migrationBuilder.DropColumn(
                name: "technical_specs",
                table: "hardware_components");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "software_components",
                newName: "retailer_code");

            migrationBuilder.RenameColumn(
                name: "revision",
                table: "hardware_components",
                newName: "retailer_code");

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                table: "software_components",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                table: "hardware_components",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }
    }
}
