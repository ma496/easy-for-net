using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class NonUniqueIndexInUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FirstName",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_LastName",
                schema: "identity",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FirstName",
                schema: "identity",
                table: "Users",
                column: "FirstName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastName",
                schema: "identity",
                table: "Users",
                column: "LastName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FirstName",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_LastName",
                schema: "identity",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FirstName",
                schema: "identity",
                table: "Users",
                column: "FirstName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastName",
                schema: "identity",
                table: "Users",
                column: "LastName",
                unique: true);
        }
    }
}
