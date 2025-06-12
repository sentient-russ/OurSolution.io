using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace os.Migrations
{
    /// <inheritdoc />
    public partial class announcements2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "status",
                schema: "Identity",
                table: "Announcements",
                newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "Identity",
                table: "Announcements",
                newName: "status");
        }
    }
}
