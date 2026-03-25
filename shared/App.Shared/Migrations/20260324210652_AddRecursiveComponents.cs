using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddRecursiveComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "software_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "hardware_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "floor_plan_anchor",
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
                name: "pc_predecessor",
                columns: table => new
                {
                    hostname = table.Column<string>(type: "text", nullable: false),
                    serial_number = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "ix_software_components_parent_id",
                table: "software_components",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_components_parent_id",
                table: "hardware_components",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_hardware_components_hardware_components_parent_id",
                table: "hardware_components",
                column: "parent_id",
                principalTable: "hardware_components",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_software_components_software_components_parent_id",
                table: "software_components",
                column: "parent_id",
                principalTable: "software_components",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_hardware_components_hardware_components_parent_id",
                table: "hardware_components");

            migrationBuilder.DropForeignKey(
                name: "fk_software_components_software_components_parent_id",
                table: "software_components");

            migrationBuilder.DropTable(
                name: "floor_plan_anchor");

            migrationBuilder.DropTable(
                name: "pc_predecessor");

            migrationBuilder.DropIndex(
                name: "ix_software_components_parent_id",
                table: "software_components");

            migrationBuilder.DropIndex(
                name: "ix_hardware_components_parent_id",
                table: "hardware_components");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "software_components");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "hardware_components");
        }
    }
}
