using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    image = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "verification",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    identifier = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verification", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<string>(type: "text", nullable: false),
                    provider_id = table.Column<string>(type: "text", nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_user_id",
                table: "account",
                column: "user_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_session_user_id",
                table: "session",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account");

            migrationBuilder.DropTable(
                name: "client_pcs");

            migrationBuilder.DropTable(
                name: "components");

            migrationBuilder.DropTable(
                name: "session");

            migrationBuilder.DropTable(
                name: "verification");

            migrationBuilder.DropTable(
                name: "user");
        }
    }
}
