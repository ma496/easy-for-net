using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The steps below are deliberately ordered - schema, then data, then the indexes the data
            // has to satisfy - so that no intermediate state of this migration is invalid. No column
            // added to an existing table is non-nullable: null is platform scope for a role, a
            // notification and a session alike, and Roles."IsDeleted" arrives with a false default. The
            // one required tenant column, TenantMemberships."TenantId", is on a table created here, so
            // it is NOT NULL from the start and there is no "add nullable, backfill, tighten" step that
            // could strand a half-migrated database.
            //
            // Requires PostgreSQL 15 or newer: IX_Roles_TenantId_Name is created NULLS NOT DISTINCT,
            // which earlier versions do not support. Without it every NULL tenant compares distinct and
            // platform-scoped role names would not be unique among themselves at all.

            // 1. Schemas and the new columns on existing tables.
            migrationBuilder.EnsureSchema(
                name: "filemanagement");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "identity",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "identity",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "identity",
                table: "Roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "notifications",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "identity",
                table: "AuthTokens",
                type: "uuid",
                nullable: true);

            // 2. The new tables.
            migrationBuilder.CreateTable(
                name: "StoredFiles",
                schema: "filemanagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemCreated = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: false),
                    IdentifierNormalized = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            // 3. Data. These steps exist for databases that already hold rows - this repository's own
            // development and test databases. A newly generated project has no migration history at
            // all: it scaffolds the schema above in one step and the data seeder, which names the same
            // bootstrap tenant identity as the insert below, supplies the first rows on first start.

            // 3a. The bootstrap tenant every pre-existing row is attributed to. Its identity is the
            // constant the seeder reads, so the seeder recognises this row instead of creating a second
            // default tenant. Status is stored as the enum member's name.
            migrationBuilder.Sql($"""
                INSERT INTO tenancy."Tenants"
                    ("Id", "SystemCreated", "Name", "Identifier", "IdentifierNormalized", "Status", "IsDeleted", "CreatedAt")
                VALUES (
                    '{TenancyConstants.BootstrapTenantId}',
                    true,
                    '{TenancyConstants.BootstrapTenantName}',
                    '{TenancyConstants.BootstrapTenantIdentifier}',
                    lower('{TenancyConstants.BootstrapTenantIdentifier}'),
                    'Active',
                    false,
                    now())
                ON CONFLICT ("Id") DO NOTHING;
                """);

            // 3b. Every existing role becomes the bootstrap tenant's, except the platform administrator
            // role, which stays at platform scope (TenantId NULL), and the Public role, which the next
            // step deletes outright. IsDeleted needs no backfill - the column arrived with a false
            // default - so after this step every role has a declared scope: a tenant, or the platform.
            migrationBuilder.Sql($"""
                UPDATE identity."Roles"
                SET "TenantId" = '{TenancyConstants.BootstrapTenantId}'
                WHERE "NameNormalized" NOT IN ('admin', 'public');
                """);

            // 3c. The Public role is removed. It existed to be granted automatically to self-service
            // sign-ups, and a role that belongs to no tenant while carrying no platform scope has no
            // declared scope at all. Its user assignments go first, then its permission grants, then
            // the role itself. The data seeder performs the same removal idempotently on every start,
            // so a database that this file never touched arrives at the same state.
            migrationBuilder.Sql("""
                DELETE FROM identity."UserRoles" ur
                USING identity."Roles" r
                WHERE ur."RoleId" = r."Id" AND r."NameNormalized" = 'public';

                DELETE FROM identity."RolePermissions" rp
                USING identity."Roles" r
                WHERE rp."RoleId" = r."Id" AND r."NameNormalized" = 'public';

                DELETE FROM identity."Roles"
                WHERE "NameNormalized" = 'public';
                """);

            // 3d. A notification addressed to a user belongs to that user's tenant. One with no
            // recipient is platform-wide and keeps its null tenant, which is what keeps it visible
            // whichever tenant its readers are acting in.
            migrationBuilder.Sql($"""
                UPDATE notifications."Notifications"
                SET "TenantId" = '{TenancyConstants.BootstrapTenantId}'
                WHERE "UserId" IS NOT NULL;
                """);

            // 3e. A row for every existing profile image, so those images keep resolving now that a
            // download needs a StoredFiles row to decide access from. They are account-owned: no
            // tenant, and the owning account named instead, which is what lets the owner read the image
            // while acting in any tenant or in none. Users."Image" already holds the generated stored
            // name, so it is both the stored and the original name here, and the content type is
            // derived from its extension the way the file service derives it on download. DISTINCT ON
            // guards the unique file-name index below against two accounts pointing at one image.
            migrationBuilder.Sql("""
                INSERT INTO filemanagement."StoredFiles"
                    ("Id", "TenantId", "OwnerUserId", "FileName", "OriginalFileName", "ContentType", "CreatedAt")
                SELECT DISTINCT ON (u."Image")
                    gen_random_uuid(),
                    NULL,
                    u."Id",
                    u."Image",
                    u."Image",
                    CASE lower(substring(u."Image" from '\.([^.]+)$'))
                        WHEN 'png' THEN 'image/png'
                        WHEN 'jpg' THEN 'image/jpeg'
                        WHEN 'jpeg' THEN 'image/jpeg'
                        WHEN 'gif' THEN 'image/gif'
                        WHEN 'webp' THEN 'image/webp'
                        WHEN 'bmp' THEN 'image/bmp'
                        WHEN 'svg' THEN 'image/svg+xml'
                        ELSE 'application/octet-stream'
                    END,
                    now()
                FROM identity."Users" u
                WHERE u."Image" IS NOT NULL AND u."Image" <> ''
                ORDER BY u."Image", u."Id";
                """);

            // 4. The indexes, last, so each one is built over the data the steps above settled on.
            migrationBuilder.DropIndex(
                name: "IX_Roles_NameNormalized",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                schema: "notifications",
                table: "Notifications");

            // Unique per tenant and deliberately unfiltered, so a name freed only by deleting a role
            // stays reserved within that tenant. NullsDistinct false is PostgreSQL's NULLS NOT
            // DISTINCT: it makes platform-scoped roles, which all carry a null tenant, unique among
            // themselves instead of escaping the constraint entirely.
            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "identity",
                table: "Roles",
                columns: new[] { "TenantId", "NameNormalized" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_UserId",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_FileName",
                schema: "filemanagement",
                table: "StoredFiles",
                column: "FileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerUserId",
                schema: "filemanagement",
                table: "StoredFiles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_TenantId",
                schema: "filemanagement",
                table: "StoredFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_CreatedAt",
                schema: "tenancy",
                table: "TenantMemberships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_User",
                schema: "tenancy",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId",
                schema: "tenancy",
                table: "TenantMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAt",
                schema: "tenancy",
                table: "Tenants",
                column: "CreatedAt");

            // Unfiltered as well: a soft-deleted tenant keeps its identifier, so an identifier freed
            // only by deletion can never be taken again.
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Identifier",
                schema: "tenancy",
                table: "Tenants",
                column: "IdentifierNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IdentifierRaw",
                schema: "tenancy",
                table: "Tenants",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                schema: "tenancy",
                table: "Tenants",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                schema: "tenancy",
                table: "Tenants",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This reverses the schema only. The deleted Public role, the tenant attributed to every
            // backfilled role and notification, the generated memberships and the stored-file rows
            // written for existing profile images are data, and none of it is restored: the columns and
            // tables that held it are dropped below.
            //
            // It can also fail outright, by design rather than by oversight: restoring the globally
            // unique IX_Roles_NameNormalized at the end is impossible once two tenants each hold a role
            // of the same name, which is exactly what the per-tenant index this migration installs
            // allows. A database that has been used as a multi-tenant one cannot be rolled back to a
            // single-tenant schema; restore a backup instead.
            migrationBuilder.DropTable(
                name: "StoredFiles",
                schema: "filemanagement");

            migrationBuilder.DropTable(
                name: "TenantMemberships",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_UserId",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "AuthTokens");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NameNormalized",
                schema: "identity",
                table: "Roles",
                column: "NameNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "notifications",
                table: "Notifications",
                column: "UserId");
        }
    }
}