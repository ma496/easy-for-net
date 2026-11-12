using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameDefaultToSystemCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Default",
                schema: "identity",
                table: "Users",
                newName: "SystemCreated");

            migrationBuilder.RenameColumn(
                name: "Default",
                schema: "identity",
                table: "Roles",
                newName: "SystemCreated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SystemCreated",
                schema: "identity",
                table: "Users",
                newName: "Default");

            migrationBuilder.RenameColumn(
                name: "SystemCreated",
                schema: "identity",
                table: "Roles",
                newName: "Default");
        }
    }
}
