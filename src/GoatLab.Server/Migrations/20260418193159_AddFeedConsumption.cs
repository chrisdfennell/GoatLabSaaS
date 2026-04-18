using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedConsumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FeedInventoryId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<double>(type: "float", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedConsumptions_FeedInventory_FeedInventoryId",
                        column: x => x.FeedInventoryId,
                        principalTable: "FeedInventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedConsumptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedConsumptions_FeedInventoryId",
                table: "FeedConsumptions",
                column: "FeedInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedConsumptions_TenantId",
                table: "FeedConsumptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedConsumptions_TenantId_FeedInventoryId_Date",
                table: "FeedConsumptions",
                columns: new[] { "TenantId", "FeedInventoryId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedConsumptions");
        }
    }
}
