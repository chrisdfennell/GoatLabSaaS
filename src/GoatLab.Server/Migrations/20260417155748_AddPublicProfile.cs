using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicContactEmail",
                table: "Tenants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicProfileEnabled",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AskingPriceCents",
                table: "Goats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsListedForSale",
                table: "Goats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SaleNotes",
                table: "Goats",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicContactEmail",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PublicProfileEnabled",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AskingPriceCents",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "IsListedForSale",
                table: "Goats");

            migrationBuilder.DropColumn(
                name: "SaleNotes",
                table: "Goats");
        }
    }
}
