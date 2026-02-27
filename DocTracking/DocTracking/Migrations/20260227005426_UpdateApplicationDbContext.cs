using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class UpdateApplicationDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents",
                column: "CreatorId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents",
                column: "CreatorId",
                principalTable: "AppUsers",
                principalColumn: "Id");
        }
    }
}
