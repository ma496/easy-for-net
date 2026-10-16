using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class StoreEnumsAsStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The integer columns are converted by name rather than by text cast, so the stored
            // values become the enum member names the string conversion reads back. A token whose
            // purpose matches no member predates the column and could never be redeemed.
            migrationBuilder.Sql("""DELETE FROM identity."Tokens" WHERE "Purpose" NOT IN (1, 2);""");
            migrationBuilder.Sql("""
                ALTER TABLE identity."Tokens" ALTER COLUMN "Purpose" TYPE text
                    USING CASE "Purpose" WHEN 1 THEN 'EmailVerification' WHEN 2 THEN 'PasswordReset' END;
                """);

            migrationBuilder.Sql("""ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" DROP DEFAULT;""");
            migrationBuilder.Sql("""
                ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" TYPE text
                    USING CASE "Scope" WHEN 1 THEN 'Platform' WHEN 2 THEN 'Both' ELSE 'Tenant' END;
                """);
            migrationBuilder.Sql("""ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" SET DEFAULT 'Tenant';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE identity."Tokens" ALTER COLUMN "Purpose" TYPE integer
                    USING CASE "Purpose" WHEN 'EmailVerification' THEN 1 WHEN 'PasswordReset' THEN 2 END;
                """);

            migrationBuilder.Sql("""ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" DROP DEFAULT;""");
            migrationBuilder.Sql("""
                ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" TYPE integer
                    USING CASE "Scope" WHEN 'Platform' THEN 1 WHEN 'Both' THEN 2 ELSE 0 END;
                """);
            migrationBuilder.Sql("""ALTER TABLE identity."Permissions" ALTER COLUMN "Scope" SET DEFAULT 0;""");
        }
    }
}
