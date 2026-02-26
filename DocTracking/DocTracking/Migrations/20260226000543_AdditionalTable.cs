using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class AdditionalTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Offices_OfficeId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "WorkerEmail",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "OriginalUserEmail",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "OfficeId",
                table: "AppUsers",
                newName: "UnitId");

            migrationBuilder.RenameIndex(
                name: "IX_AppUsers_OfficeId",
                table: "AppUsers",
                newName: "IX_AppUsers_UnitId");

            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentUnitId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextUnitId",
                table: "Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "DocumentLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "DocumentLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Unit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OfficeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Unit_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatorId",
                table: "Documents",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CurrentUnitId",
                table: "Documents",
                column: "CurrentUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_NextUnitId",
                table: "Documents",
                column: "NextUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_AppUserId",
                table: "DocumentLogs",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_UnitId",
                table: "DocumentLogs",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_OfficeId",
                table: "Unit",
                column: "OfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Unit_UnitId",
                table: "AppUsers",
                column: "UnitId",
                principalTable: "Unit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_Unit_UnitId",
                table: "DocumentLogs",
                column: "UnitId",
                principalTable: "Unit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents",
                column: "CreatorId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Unit_CurrentUnitId",
                table: "Documents",
                column: "CurrentUnitId",
                principalTable: "Unit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Unit_NextUnitId",
                table: "Documents",
                column: "NextUnitId",
                principalTable: "Unit",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Unit_UnitId",
                table: "AppUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_AppUsers_AppUserId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_Unit_UnitId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AppUsers_CreatorId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Unit_CurrentUnitId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Unit_NextUnitId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Unit");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CreatorId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CurrentUnitId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_NextUnitId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLogs_AppUserId",
                table: "DocumentLogs");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLogs_UnitId",
                table: "DocumentLogs");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CurrentUnitId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "NextUnitId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "DocumentLogs");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "DocumentLogs");

            migrationBuilder.RenameColumn(
                name: "UnitId",
                table: "AppUsers",
                newName: "OfficeId");

            migrationBuilder.RenameIndex(
                name: "IX_AppUsers_UnitId",
                table: "AppUsers",
                newName: "IX_AppUsers_OfficeId");

            migrationBuilder.AddColumn<string>(
                name: "WorkerEmail",
                table: "Offices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalUserEmail",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Offices_OfficeId",
                table: "AppUsers",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id");
        }
    }
}
