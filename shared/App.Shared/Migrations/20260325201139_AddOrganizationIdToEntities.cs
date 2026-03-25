using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationIdToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "machines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organization_id",
                schema: "backend",
                table: "client_pcs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "machines");

            migrationBuilder.DropColumn(
                name: "organization_id",
                schema: "backend",
                table: "client_pcs");
        }
    }
}
