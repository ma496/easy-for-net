using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EditionId",
                schema: "tenancy",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Editions",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameNormalized = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Editions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureValues",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_EditionId",
                schema: "tenancy",
                table: "Tenants",
                column: "EditionId");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_CreatedAt",
                schema: "tenancy",
                table: "Editions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_DisplayOrder",
                schema: "tenancy",
                table: "Editions",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Editions_Name",
                schema: "tenancy",
                table: "Editions",
                column: "NameNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureValues_Name_ProviderName_ProviderKey",
                schema: "tenancy",
                table: "FeatureValues",
                columns: new[] { "Name", "ProviderName", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureValues_ProviderName_ProviderKey",
                schema: "tenancy",
                table: "FeatureValues",
                columns: new[] { "ProviderName", "ProviderKey" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Editions_EditionId",
                schema: "tenancy",
                table: "Tenants",
                column: "EditionId",
                principalSchema: "tenancy",
                principalTable: "Editions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Editions_EditionId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "Editions",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "FeatureValues",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_EditionId",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EditionId",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
