using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class UniqueRawNormalizedIndexes : Migration
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
                name: "IX_Tenants_IdentifierRaw",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles");

            migrationBuilder.RenameIndex(
                name: "IX_Tenants_Identifier",
                schema: "tenancy",
                table: "Tenants",
                newName: "IX_Tenants_IdentifierNormalized");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "identity",
                table: "Roles",
                newName: "IX_Roles_TenantId_NameNormalized");

            migrationBuilder.RenameIndex(
                name: "IX_Editions_Name",
                schema: "tenancy",
                table: "Editions",
                newName: "IX_Editions_NameNormalized");

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
                name: "IX_Tenants_Identifier",
                schema: "tenancy",
                table: "Tenants",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "identity",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Editions_Name",
                schema: "tenancy",
                table: "Editions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "IX_Tenants_Identifier",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Editions_Name",
                schema: "tenancy",
                table: "Editions");

            migrationBuilder.RenameIndex(
                name: "IX_Tenants_IdentifierNormalized",
                schema: "tenancy",
                table: "Tenants",
                newName: "IX_Tenants_Identifier");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_TenantId_NameNormalized",
                schema: "identity",
                table: "Roles",
                newName: "IX_Roles_TenantId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Editions_NameNormalized",
                schema: "tenancy",
                table: "Editions",
                newName: "IX_Editions_Name");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "identity",
                table: "Users",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IdentifierRaw",
                schema: "tenancy",
                table: "Tenants",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "identity",
                table: "Roles",
                column: "Name");
        }
    }
}
