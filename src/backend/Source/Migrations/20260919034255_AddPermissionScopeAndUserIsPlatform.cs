using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionScopeAndUserIsPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatform",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                schema: "identity",
                table: "Permissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Carries the platform tier over from how it used to be recognised: an account holding a
            // role that belongs to no tenant. That is the reading every platform check used before the
            // tier became a column, and it has to run here because the seeder is about to delete the
            // permission those checks were written against - after which the old reading would be
            // impossible to reconstruct. The seeder reconciles the stored permission scopes on the same
            // startup, so no backfill is needed for the other column.
            migrationBuilder.Sql("""
                UPDATE identity."Users" AS u
                SET "IsPlatform" = TRUE
                WHERE EXISTS (
                    SELECT 1
                    FROM identity."UserRoles" AS ur
                    JOIN identity."Roles" AS r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = u."Id"
                      AND r."TenantId" IS NULL
                      AND r."IsDeleted" = FALSE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatform",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "identity",
                table: "Permissions");
        }
    }
}
