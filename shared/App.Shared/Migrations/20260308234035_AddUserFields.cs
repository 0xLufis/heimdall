using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ban_expires",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ban_reason",
                table: "user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "banned",
                table: "user",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "user",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ban_expires",
                table: "user");

            migrationBuilder.DropColumn(
                name: "ban_reason",
                table: "user");

            migrationBuilder.DropColumn(
                name: "banned",
                table: "user");

            migrationBuilder.DropColumn(
                name: "role",
                table: "user");
        }
    }
}
