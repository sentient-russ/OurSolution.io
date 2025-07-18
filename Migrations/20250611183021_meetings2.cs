﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace os.Migrations
{
    /// <inheritdoc />
    public partial class meetings2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleMapsLink",
                schema: "Identity",
                table: "Meetings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleMapsLink",
                schema: "Identity",
                table: "Meetings");
        }
    }
}
