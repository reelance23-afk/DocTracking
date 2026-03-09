using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OfficeId",
                table: "AppUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_OfficeId",
                table: "AppUsers",
                column: "OfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Offices_OfficeId",
                table: "AppUsers",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Offices_OfficeId",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_OfficeId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "OfficeId",
                table: "AppUsers");
        }
    }
}
