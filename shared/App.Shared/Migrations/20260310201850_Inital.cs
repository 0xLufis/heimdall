using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class Inital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_pcs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    machine_identifier = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mac_address = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    last_online = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hardware_config = table.Column<HardwareConfig>(type: "jsonb", nullable: false),
                    software_config = table.Column<SoftwareConfig>(type: "jsonb", nullable: false),
                    custom_data_points = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    predecessors = table.Column<List<PcPredecessor>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_pcs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    admin_managed_fields = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_hostname",
                table: "client_pcs",
                column: "hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_pcs_mac_address",
                table: "client_pcs",
                column: "mac_address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_pcs");

            migrationBuilder.DropTable(
                name: "components");
        }
    }
}
