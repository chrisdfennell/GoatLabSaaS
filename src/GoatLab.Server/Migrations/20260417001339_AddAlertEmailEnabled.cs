using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEmailEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default to true so existing tenants are opted in (matches the
            // C# property default). Owners can disable on /farm-settings.
            migrationBuilder.AddColumn<bool>(
                name: "AlertEmailEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertEmailEnabled",
                table: "Tenants");
        }
    }
}
