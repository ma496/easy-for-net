using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class NotificationReadCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationVisits_UserId_VisitedAt",
                schema: "notifications",
                table: "NotificationVisits");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAt",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_MessageKey",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TitleKey",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                schema: "notifications",
                table: "NotificationVisits",
                type: "boolean",
                nullable: false,
                // Existing visit rows were written when a user opened a notification, so they migrate as read.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "NotificationReadCursors",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAllAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReadCursors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Audience",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"UserId\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Personal",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, false, true },
                filter: "\"UserId\" IS NOT NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReadCursors_UserId_TenantId",
                schema: "notifications",
                table: "NotificationReadCursors",
                columns: new[] { "UserId", "TenantId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationReadCursors",
                schema: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Audience",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Personal",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsRead",
                schema: "notifications",
                table: "NotificationVisits");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationVisits_UserId_VisitedAt",
                schema: "notifications",
                table: "NotificationVisits",
                columns: new[] { "UserId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                schema: "notifications",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MessageKey",
                schema: "notifications",
                table: "Notifications",
                column: "MessageKey");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TitleKey",
                schema: "notifications",
                table: "Notifications",
                column: "TitleKey");
        }
    }
}
