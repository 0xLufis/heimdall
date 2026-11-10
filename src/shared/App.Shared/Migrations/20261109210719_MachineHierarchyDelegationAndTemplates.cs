using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MachineHierarchyDelegationAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "business_unit_group_id",
                schema: "backend",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cell_type",
                schema: "backend",
                table: "stations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "line_group_id",
                schema: "backend",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner",
                schema: "backend",
                table: "stations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_user_id",
                schema: "backend",
                table: "stations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_machine_id",
                schema: "backend",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_group_id",
                schema: "backend",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "spoc",
                schema: "backend",
                table: "stations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "spoc_user_id",
                schema: "backend",
                table: "stations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sub_line_group_id",
                schema: "backend",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_machine_index",
                schema: "backend",
                table: "stations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "technologies",
                schema: "backend",
                table: "stations",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_complete_machine_dedication",
                schema: "backend",
                table: "delegation_recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_technology_primary",
                schema: "backend",
                table: "delegation_recipes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "machine_group_ids_json",
                schema: "backend",
                table: "delegation_recipes",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "maintenance_group_ids_json",
                schema: "backend",
                table: "delegation_recipes",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "technology",
                schema: "backend",
                table: "delegation_recipes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delegation_machine_groups",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegation_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegation_machine_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_delegation_machine_groups_delegation_recipes_delegation_rec",
                        column: x => x.delegation_recipe_id,
                        principalSchema: "backend",
                        principalTable: "delegation_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delegation_machine_groups_machine_groups_machine_group_id",
                        column: x => x.machine_group_id,
                        principalSchema: "backend",
                        principalTable: "machine_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delegation_maintenance_groups",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegation_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maintenance_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegation_maintenance_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_delegation_maintenance_groups_delegation_recipes_delegation",
                        column: x => x.delegation_recipe_id,
                        principalSchema: "backend",
                        principalTable: "delegation_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delegation_maintenance_groups_personnel_groups_maintenance_",
                        column: x => x.maintenance_group_id,
                        principalSchema: "backend",
                        principalTable: "personnel_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generic_templates",
                schema: "backend",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    extends_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    schema_json = table.Column<string>(type: "text", nullable: false),
                    default_values_json = table.Column<string>(type: "text", nullable: false),
                    ui_config_json = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generic_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_generic_templates_generic_templates_extends_template_id",
                        column: x => x.extends_template_id,
                        principalSchema: "backend",
                        principalTable: "generic_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stations_business_unit_group_id",
                schema: "backend",
                table: "stations",
                column: "business_unit_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_stations_line_group_id",
                schema: "backend",
                table: "stations",
                column: "line_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_stations_parent_machine_id",
                schema: "backend",
                table: "stations",
                column: "parent_machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_stations_project_group_id",
                schema: "backend",
                table: "stations",
                column: "project_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_stations_sub_line_group_id",
                schema: "backend",
                table: "stations",
                column: "sub_line_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_machine_groups_parent_id",
                schema: "backend",
                table: "machine_groups",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_delegation_machine_groups_delegation_recipe_id_machine_grou",
                schema: "backend",
                table: "delegation_machine_groups",
                columns: new[] { "delegation_recipe_id", "machine_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delegation_machine_groups_machine_group_id",
                schema: "backend",
                table: "delegation_machine_groups",
                column: "machine_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_delegation_maintenance_groups_delegation_recipe_id_maintena",
                schema: "backend",
                table: "delegation_maintenance_groups",
                columns: new[] { "delegation_recipe_id", "maintenance_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delegation_maintenance_groups_maintenance_group_id",
                schema: "backend",
                table: "delegation_maintenance_groups",
                column: "maintenance_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_generic_templates_category",
                schema: "backend",
                table: "generic_templates",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_generic_templates_code",
                schema: "backend",
                table: "generic_templates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_generic_templates_extends_template_id",
                schema: "backend",
                table: "generic_templates",
                column: "extends_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_generic_templates_organization_id",
                schema: "backend",
                table: "generic_templates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_generic_templates_template_type",
                schema: "backend",
                table: "generic_templates",
                column: "template_type");

            migrationBuilder.AddForeignKey(
                name: "fk_machine_groups_machine_groups_parent_id",
                schema: "backend",
                table: "machine_groups",
                column: "parent_id",
                principalSchema: "backend",
                principalTable: "machine_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stations_machine_groups_business_unit_group_id",
                schema: "backend",
                table: "stations",
                column: "business_unit_group_id",
                principalSchema: "backend",
                principalTable: "machine_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_stations_machine_groups_line_group_id",
                schema: "backend",
                table: "stations",
                column: "line_group_id",
                principalSchema: "backend",
                principalTable: "machine_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_stations_machine_groups_project_group_id",
                schema: "backend",
                table: "stations",
                column: "project_group_id",
                principalSchema: "backend",
                principalTable: "machine_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_stations_machine_groups_sub_line_group_id",
                schema: "backend",
                table: "stations",
                column: "sub_line_group_id",
                principalSchema: "backend",
                principalTable: "machine_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_stations_stations_parent_machine_id",
                schema: "backend",
                table: "stations",
                column: "parent_machine_id",
                principalSchema: "backend",
                principalTable: "stations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_machine_groups_machine_groups_parent_id",
                schema: "backend",
                table: "machine_groups");

            migrationBuilder.DropForeignKey(
                name: "fk_stations_machine_groups_business_unit_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "fk_stations_machine_groups_line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "fk_stations_machine_groups_project_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "fk_stations_machine_groups_sub_line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "fk_stations_stations_parent_machine_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropTable(
                name: "delegation_machine_groups",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "delegation_maintenance_groups",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "generic_templates",
                schema: "backend");

            migrationBuilder.DropIndex(
                name: "ix_stations_business_unit_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "ix_stations_line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "ix_stations_parent_machine_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "ix_stations_project_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "ix_stations_sub_line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "ix_machine_groups_parent_id",
                schema: "backend",
                table: "machine_groups");

            migrationBuilder.DropColumn(
                name: "business_unit_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "cell_type",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "owner",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "parent_machine_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "project_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "spoc",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "spoc_user_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "sub_line_group_id",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "sub_machine_index",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "technologies",
                schema: "backend",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "is_complete_machine_dedication",
                schema: "backend",
                table: "delegation_recipes");

            migrationBuilder.DropColumn(
                name: "is_technology_primary",
                schema: "backend",
                table: "delegation_recipes");

            migrationBuilder.DropColumn(
                name: "machine_group_ids_json",
                schema: "backend",
                table: "delegation_recipes");

            migrationBuilder.DropColumn(
                name: "maintenance_group_ids_json",
                schema: "backend",
                table: "delegation_recipes");

            migrationBuilder.DropColumn(
                name: "technology",
                schema: "backend",
                table: "delegation_recipes");
        }
    }
}
