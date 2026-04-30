using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketplaceAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    BreedSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    MinPriceCents = table.Column<int>(type: "int", nullable: true),
                    MaxPriceCents = table.Column<int>(type: "int", nullable: true),
                    StateMatch = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UnsubscribeToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnsubscribedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplaceAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAlerts_BreedSlug_IsActive",
                table: "MarketplaceAlerts",
                columns: new[] { "BreedSlug", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketplaceAlerts_UnsubscribeToken",
                table: "MarketplaceAlerts",
                column: "UnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketplaceAlerts");
        }
    }
}
