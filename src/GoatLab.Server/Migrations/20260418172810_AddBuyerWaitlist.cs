using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerWaitlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    BreedPreference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SexPreference = table.Column<int>(type: "int", nullable: true),
                    ColorPreference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepositCents = table.Column<int>(type: "int", nullable: false),
                    DepositPaid = table.Column<bool>(type: "bit", nullable: false),
                    DepositReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledSaleId = table.Column<int>(type: "int", nullable: true),
                    FulfilledGoatId = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Goats_FulfilledGoatId",
                        column: x => x.FulfilledGoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Sales_FulfilledSaleId",
                        column: x => x.FulfilledSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_CustomerId",
                table: "WaitlistEntries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_FulfilledGoatId",
                table: "WaitlistEntries",
                column: "FulfilledGoatId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_FulfilledSaleId",
                table: "WaitlistEntries",
                column: "FulfilledSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_TenantId",
                table: "WaitlistEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_TenantId_Status_Priority",
                table: "WaitlistEntries",
                columns: new[] { "TenantId", "Status", "Priority" });

            // Backfill PlanFeature row for AppFeature.BuyerWaitlist (=19) across
            // the three seeded plans. Paid-tier upsell — Homestead off, Farm/Dairy on.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Plans] WHERE Id IN (1,2,3))
                BEGIN
                    INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                    SELECT v.PlanId, v.Feature, v.Enabled
                    FROM (VALUES
                        (1, 19, CAST(0 AS bit)),  -- Homestead: BuyerWaitlist off
                        (2, 19, CAST(1 AS bit)),  -- Farm: BuyerWaitlist on
                        (3, 19, CAST(1 AS bit))   -- Dairy: BuyerWaitlist on
                    ) v(PlanId, Feature, Enabled)
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [PlanFeatures] pf
                        WHERE pf.PlanId = v.PlanId AND pf.Feature = v.Feature
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [PlanFeatures] WHERE Feature = 19;");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");
        }
    }
}
