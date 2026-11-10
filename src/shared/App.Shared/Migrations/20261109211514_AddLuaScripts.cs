using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddLuaScripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lua_scripts",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    trigger_type = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_execution_success = table.Column<bool>(type: "boolean", nullable: true),
                    last_execution_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lua_scripts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lua_scripts_trigger_type_is_active",
                schema: "backend",
                table: "lua_scripts",
                columns: new[] { "trigger_type", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lua_scripts",
                schema: "backend");
        }
    }
}
