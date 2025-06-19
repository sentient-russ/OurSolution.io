using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace os.Migrations
{
    /// <inheritdoc />
    public partial class ReplacedNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReplacedNames",
                schema: "Identity",
                table: "Speakers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacedNames",
                schema: "Identity",
                table: "Speakers");
        }
    }
}
