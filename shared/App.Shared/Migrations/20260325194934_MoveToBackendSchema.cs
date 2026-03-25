using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MoveToBackendSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "backend");

            migrationBuilder.RenameTable(
                name: "user_roles",
                newName: "user_roles",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "suppliers",
                newName: "suppliers",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "software_components",
                newName: "software_components",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "pc_predecessor",
                newName: "pc_predecessor",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "manufacturers",
                newName: "manufacturers",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "machines",
                newName: "machines",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "hardware_components",
                newName: "hardware_components",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "floor_plans",
                newName: "floor_plans",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "floor_plan_anchor",
                newName: "floor_plan_anchor",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "components",
                newName: "components",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "ClientPcMachine",
                newName: "ClientPcMachine",
                newSchema: "backend");

            migrationBuilder.RenameTable(
                name: "client_pcs",
                newName: "client_pcs",
                newSchema: "backend");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "user_roles",
                schema: "backend",
                newName: "user_roles");

            migrationBuilder.RenameTable(
                name: "suppliers",
                schema: "backend",
                newName: "suppliers");

            migrationBuilder.RenameTable(
                name: "software_components",
                schema: "backend",
                newName: "software_components");

            migrationBuilder.RenameTable(
                name: "pc_predecessor",
                schema: "backend",
                newName: "pc_predecessor");

            migrationBuilder.RenameTable(
                name: "manufacturers",
                schema: "backend",
                newName: "manufacturers");

            migrationBuilder.RenameTable(
                name: "machines",
                schema: "backend",
                newName: "machines");

            migrationBuilder.RenameTable(
                name: "hardware_components",
                schema: "backend",
                newName: "hardware_components");

            migrationBuilder.RenameTable(
                name: "floor_plans",
                schema: "backend",
                newName: "floor_plans");

            migrationBuilder.RenameTable(
                name: "floor_plan_anchor",
                schema: "backend",
                newName: "floor_plan_anchor");

            migrationBuilder.RenameTable(
                name: "components",
                schema: "backend",
                newName: "components");

            migrationBuilder.RenameTable(
                name: "ClientPcMachine",
                schema: "backend",
                newName: "ClientPcMachine");

            migrationBuilder.RenameTable(
                name: "client_pcs",
                schema: "backend",
                newName: "client_pcs");
        }
    }
}
