using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AdjustIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles");

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

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Description",
                schema: "identity",
                table: "Roles",
                column: "Description",
                unique: true);
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

            migrationBuilder.DropIndex(
                name: "IX_Roles_Description",
                schema: "identity",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "identity",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name",
                unique: true);
        }
    }
}
