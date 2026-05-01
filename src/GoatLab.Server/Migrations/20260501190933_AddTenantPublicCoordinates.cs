using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPublicCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PublicLatitude",
                table: "Tenants",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PublicLongitude",
                table: "Tenants",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicLatitude",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PublicLongitude",
                table: "Tenants");
        }
    }
}
