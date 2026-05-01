using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerInquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuyerInquiries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    BuyerEmail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BuyerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BuyerPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UnreadForSeller = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerInquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyerInquiries_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuyerInquiries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BuyerInquiryMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InquiryId = table.Column<int>(type: "int", nullable: false),
                    FromSeller = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyerInquiryMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyerInquiryMessages_BuyerInquiries_InquiryId",
                        column: x => x.InquiryId,
                        principalTable: "BuyerInquiries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerInquiries_GoatId",
                table: "BuyerInquiries",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_BuyerInquiries_TenantId",
                table: "BuyerInquiries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BuyerInquiries_TenantId_GoatId_BuyerEmail",
                table: "BuyerInquiries",
                columns: new[] { "TenantId", "GoatId", "BuyerEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyerInquiries_TenantId_LastMessageAt",
                table: "BuyerInquiries",
                columns: new[] { "TenantId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BuyerInquiryMessages_InquiryId_CreatedAt",
                table: "BuyerInquiryMessages",
                columns: new[] { "InquiryId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuyerInquiryMessages");

            migrationBuilder.DropTable(
                name: "BuyerInquiries");
        }
    }
}
