using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ClientPcManyToManyMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_client_pcs_machines_machine_id",
                table: "client_pcs");

            migrationBuilder.DropIndex(
                name: "ix_client_pcs_machine_id",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "client_pcs");

            migrationBuilder.CreateTable(
                name: "ClientPcMachine",
                columns: table => new
                {
                    client_pcs_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machines_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_pc_machine", x => new { x.client_pcs_id, x.machines_id });
                    table.ForeignKey(
                        name: "fk_client_pc_machine_client_pcs_client_pcs_id",
                        column: x => x.client_pcs_id,
                        principalTable: "client_pcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_client_pc_machine_machines_machines_id",
                        column: x => x.machines_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_pc_machine_machines_id",
                table: "ClientPcMachine",
                column: "machines_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientPcMachine");

            migrationBuilder.AddColumn<Guid>(
                name: "machine_id",
                table: "client_pcs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_machine_id",
                table: "client_pcs",
                column: "machine_id");

            migrationBuilder.AddForeignKey(
                name: "fk_client_pcs_machines_machine_id",
                table: "client_pcs",
                column: "machine_id",
                principalTable: "machines",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
