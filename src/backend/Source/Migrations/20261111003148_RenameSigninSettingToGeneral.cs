using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameSigninSettingToGeneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Signin setting became part of the General setting; keep every saved override.
            migrationBuilder.Sql("UPDATE settings.\"SettingValues\" SET \"Name\" = 'General' WHERE \"Name\" = 'Signin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE settings.\"SettingValues\" SET \"Name\" = 'Signin' WHERE \"Name\" = 'General';");
        }
    }
}
