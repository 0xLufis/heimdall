using System;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRefinementsAndMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cost_center",
                schema: "backend",
                table: "inventory_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cost_center_ou",
                schema: "backend",
                table: "inventory_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "backend",
                table: "inventory_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_creator",
                schema: "backend",
                table: "inventory_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_updater",
                schema: "backend",
                table: "inventory_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "backend",
                table: "inventory_components",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<AlertingLimits>(
                name: "alerting_limits",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DiskSpaceInfo>(
                name: "free_disk_space",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<ResourceMonitoringConfig>(
                name: "monitoring_config",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<ResourceAverages>(
                name: "resource_averages",
                schema: "backend",
                table: "client_pcs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alerting_limits",
                schema: "backend",
                columns: table => new
                {
                    cpu_threshold = table.Column<double>(type: "double precision", nullable: false),
                    ram_threshold = table.Column<double>(type: "double precision", nullable: false),
                    disk_free_space_min_gb = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "disk_space_info",
                schema: "backend",
                columns: table => new
                {
                    total_free_gb = table.Column<double>(type: "double precision", nullable: false),
                    os_drive_free_gb = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "resource_averages",
                schema: "backend",
                columns: table => new
                {
                    cpu_usage_average = table.Column<double>(type: "double precision", nullable: false),
                    ram_usage_average = table.Column<double>(type: "double precision", nullable: false),
                    disk_io_average = table.Column<double>(type: "double precision", nullable: false),
                    last_calculated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "resource_monitoring_config",
                schema: "backend",
                columns: table => new
                {
                    sampling_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerting_limits",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "disk_space_info",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "resource_averages",
                schema: "backend");

            migrationBuilder.DropTable(
                name: "resource_monitoring_config",
                schema: "backend");

            migrationBuilder.DropColumn(
                name: "cost_center",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "cost_center_ou",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "entity_creator",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "entity_updater",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "backend",
                table: "inventory_components");

            migrationBuilder.DropColumn(
                name: "alerting_limits",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "free_disk_space",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "monitoring_config",
                schema: "backend",
                table: "client_pcs");

            migrationBuilder.DropColumn(
                name: "resource_averages",
                schema: "backend",
                table: "client_pcs");
        }
    }
}
