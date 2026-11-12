using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class PkiTrustStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "private_key_pem",
                schema: "backend",
                table: "client_certificates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trusted_ca_certificates",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_self_signed = table.Column<bool>(type: "boolean", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raw_pem = table.Column<string>(type: "text", nullable: true),
                    csr_pem = table.Column<string>(type: "text", nullable: true),
                    private_key_pem = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trusted_ca_certificates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_ca_certificates_source_status",
                schema: "backend",
                table: "trusted_ca_certificates",
                columns: new[] { "source", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_ca_certificates_thumbprint",
                schema: "backend",
                table: "trusted_ca_certificates",
                column: "thumbprint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trusted_ca_certificates",
                schema: "backend");

            migrationBuilder.DropColumn(
                name: "private_key_pem",
                schema: "backend",
                table: "client_certificates");
        }
    }
}
