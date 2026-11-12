using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class UploadFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "file-management",
                table: "UploadFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                schema: "file-management",
                table: "UploadFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Size",
                schema: "file-management",
                table: "UploadFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "file-management",
                table: "UploadFiles");

            migrationBuilder.DropColumn(
                name: "Extension",
                schema: "file-management",
                table: "UploadFiles");

            migrationBuilder.DropColumn(
                name: "Size",
                schema: "file-management",
                table: "UploadFiles");
        }
    }
}
