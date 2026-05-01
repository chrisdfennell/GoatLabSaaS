using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuyerAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuyerSignInTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerSignInTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuyerSavedListings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerAccountId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerSavedListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyerSavedListings_BuyerAccounts_BuyerAccountId",
                        column: x => x.BuyerAccountId,
                        principalTable: "BuyerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuyerSavedListings_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerAccounts_Email",
                table: "BuyerAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyerSavedListings_BuyerAccountId_GoatId",
                table: "BuyerSavedListings",
                columns: new[] { "BuyerAccountId", "GoatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyerSavedListings_GoatId",
                table: "BuyerSavedListings",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_BuyerSignInTokens_Email_ExpiresAt",
                table: "BuyerSignInTokens",
                columns: new[] { "Email", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerSignInTokens_TokenHash",
                table: "BuyerSignInTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuyerSavedListings");

            migrationBuilder.DropTable(
                name: "BuyerSignInTokens");

            migrationBuilder.DropTable(
                name: "BuyerAccounts");
        }
    }
}
