using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class Reaply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Unit_UnitId",
                table: "AppUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_Unit_UnitId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Unit_CurrentUnitId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Unit_NextUnitId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Unit_Offices_OfficeId",
                table: "Unit");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Unit",
                table: "Unit");

            migrationBuilder.RenameTable(
                name: "Unit",
                newName: "Units");

            migrationBuilder.RenameIndex(
                name: "IX_Unit_OfficeId",
                table: "Units",
                newName: "IX_Units_OfficeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Units",
                table: "Units",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Units_UnitId",
                table: "AppUsers",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_Units_UnitId",
                table: "DocumentLogs",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Units_CurrentUnitId",
                table: "Documents",
                column: "CurrentUnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Units_NextUnitId",
                table: "Documents",
                column: "NextUnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Units_Offices_OfficeId",
                table: "Units",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_Units_UnitId",
                table: "AppUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_Units_UnitId",
                table: "DocumentLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Units_CurrentUnitId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Units_NextUnitId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_Offices_OfficeId",
                table: "Units");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Units",
                table: "Units");

            migrationBuilder.RenameTable(
                name: "Units",
                newName: "Unit");

            migrationBuilder.RenameIndex(
                name: "IX_Units_OfficeId",
                table: "Unit",
                newName: "IX_Unit_OfficeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Unit",
                table: "Unit",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_Unit_UnitId",
                table: "AppUsers",
                column: "UnitId",
                principalTable: "Unit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_Unit_UnitId",
                table: "DocumentLogs",
                column: "UnitId",
                principalTable: "Unit",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Unit_Offices_OfficeId",
                table: "Unit",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
