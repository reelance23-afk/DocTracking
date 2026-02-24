using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_Offices_OfficeId",
                table: "DocumentLogs");

            migrationBuilder.AlterColumn<int>(
                name: "OfficeId",
                table: "DocumentLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OfficeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUsers_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_OfficeId",
                table: "AppUsers",
                column: "OfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_Offices_OfficeId",
                table: "DocumentLogs",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLogs_Offices_OfficeId",
                table: "DocumentLogs");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.AlterColumn<int>(
                name: "OfficeId",
                table: "DocumentLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLogs_Offices_OfficeId",
                table: "DocumentLogs",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
