using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class OfficeHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOfficeHead",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOfficeHead",
                table: "AppUsers");
        }
    }
}
