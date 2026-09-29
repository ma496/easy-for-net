using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTokenSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthTokens_UserId",
                schema: "identity",
                table: "AuthTokens");

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                schema: "identity",
                table: "AuthTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthTokens_UserId_SessionId",
                schema: "identity",
                table: "AuthTokens",
                columns: new[] { "UserId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthTokens_UserId_SessionId",
                schema: "identity",
                table: "AuthTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "identity",
                table: "AuthTokens");

            migrationBuilder.CreateIndex(
                name: "IX_AuthTokens_UserId",
                schema: "identity",
                table: "AuthTokens",
                column: "UserId");
        }
    }
}
