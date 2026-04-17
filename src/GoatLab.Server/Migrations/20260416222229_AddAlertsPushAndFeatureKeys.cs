using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsPushAndFeatureKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    DeepLink = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DismissedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TenantId",
                table: "Alerts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TenantId_DismissedAt_CreatedAt",
                table: "Alerts",
                columns: new[] { "TenantId", "DismissedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TenantId_Type_EntityType_EntityId",
                table: "Alerts",
                columns: new[] { "TenantId", "Type", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_TenantId",
                table: "PushSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_TenantId_UserId",
                table: "PushSubscriptions",
                columns: new[] { "TenantId", "UserId" });

            // Backfill PlanFeature rows for the three new AppFeature keys
            // (14=SmartAlerts, 15=PushNotifications, 16=PdfDocuments) across the
            // three seeded plans (1=Homestead, 2=Farm, 3=Dairy). Guarded so this
            // is a no-op on databases where the plans were renamed or removed.
            // Homestead gets SmartAlerts (in-app value) but not push or PDFs —
            // those are paid-tier upsells. Farm and Dairy get all three.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Plans] WHERE Id IN (1,2,3))
                BEGIN
                    INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                    SELECT v.PlanId, v.Feature, v.Enabled
                    FROM (VALUES
                        (1, 14, CAST(1 AS bit)),  -- Homestead: SmartAlerts on
                        (1, 15, CAST(0 AS bit)),  -- Homestead: PushNotifications off
                        (1, 16, CAST(0 AS bit)),  -- Homestead: PdfDocuments off
                        (2, 14, CAST(1 AS bit)),  -- Farm: SmartAlerts on
                        (2, 15, CAST(1 AS bit)),  -- Farm: PushNotifications on
                        (2, 16, CAST(1 AS bit)),  -- Farm: PdfDocuments on
                        (3, 14, CAST(1 AS bit)),  -- Dairy: SmartAlerts on
                        (3, 15, CAST(1 AS bit)),  -- Dairy: PushNotifications on
                        (3, 16, CAST(1 AS bit))   -- Dairy: PdfDocuments on
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
            migrationBuilder.Sql(
                "DELETE FROM [PlanFeatures] WHERE Feature IN (14, 15, 16);");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");
        }
    }
}
