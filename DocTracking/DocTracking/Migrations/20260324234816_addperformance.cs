using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class addperformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Documents_LastActionDate",
                table: "Documents",
                column: "LastActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_AppUserId_Time",
                table: "AppNotifications",
                columns: new[] { "AppUserId", "Time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_LastActionDate",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_AppNotifications_AppUserId_Time",
                table: "AppNotifications");
        }
    }
}
