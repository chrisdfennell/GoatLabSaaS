using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansAndFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Plans and PlanFeatures tables.
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceMonthlyCents = table.Column<int>(type: "int", nullable: false),
                    StripePriceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    MaxGoats = table.Column<int>(type: "int", nullable: true),
                    MaxUsers = table.Column<int>(type: "int", nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => new { x.PlanId, x.Feature });
                    table.ForeignKey(
                        name: "FK_PlanFeatures_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Slug",
                table: "Plans",
                column: "Slug",
                unique: true);

            // 2. Seed the three default plans with explicit IDs so the enum values
            //    (0=Homestead, 1=Farm, 2=Dairy) can be remapped to real FK ids
            //    (+1 offset → 1, 2, 3) in a single UPDATE.
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [Plans] ON;
                INSERT INTO [Plans] (Id, Name, Slug, Description, PriceMonthlyCents, StripePriceId, TrialDays, MaxGoats, MaxUsers, IsPublic, IsActive, DisplayOrder, CreatedAt, UpdatedAt) VALUES
                  (1, 'Homestead', 'homestead', 'For small herds just getting organized.',    0, NULL,  0,   10,    1, 1, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
                  (2, 'Farm',      'farm',      'For working farms tracking production.',  1900, NULL, 14, NULL,    3, 1, 1, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
                  (3, 'Dairy',     'dairy',     'For commercial dairies and breeders.',    4900, NULL,  0, NULL, NULL, 1, 1, 3, SYSUTCDATETIME(), SYSUTCDATETIME());
                SET IDENTITY_INSERT [Plans] OFF;
            ");

            // 3. Seed default plan features. Feature enum values:
            //    0 Goats, 1 Health, 2 Breeding, 3 Milk, 4 Sales, 5 Finance,
            //    6 Inventory, 7 Calendar, 8 Map, 9 CareGuide, 10 Barns,
            //    11 AdvancedReports, 12 ShowRecords, 13 DataExport.
            migrationBuilder.Sql(@"
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled) VALUES
                  -- Homestead: core only
                  (1,0,1),(1,1,1),(1,2,1),(1,3,1),(1,7,1),(1,8,1),(1,9,1),(1,10,1),
                  -- Farm: core + production modules + data export
                  (2,0,1),(2,1,1),(2,2,1),(2,3,1),(2,4,1),(2,5,1),(2,6,1),(2,7,1),(2,8,1),(2,9,1),(2,10,1),(2,13,1),
                  -- Dairy: everything
                  (3,0,1),(3,1,1),(3,2,1),(3,3,1),(3,4,1),(3,5,1),(3,6,1),(3,7,1),(3,8,1),(3,9,1),(3,10,1),(3,11,1),(3,12,1),(3,13,1);
            ");

            // 4. Rename Tenants.Plan → Tenants.PlanId, then remap enum values to real ids.
            migrationBuilder.RenameColumn(
                name: "Plan",
                table: "Tenants",
                newName: "PlanId");

            migrationBuilder.Sql("UPDATE [Tenants] SET PlanId = PlanId + 1;");

            // 5. Finally wire up index + FK now that values are valid.
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Plans_PlanId",
                table: "Tenants",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Plans_PlanId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants");

            // Reverse the remap before dropping the FK / renaming back.
            migrationBuilder.Sql("UPDATE [Tenants] SET PlanId = PlanId - 1;");

            migrationBuilder.RenameColumn(
                name: "PlanId",
                table: "Tenants",
                newName: "Plan");

            migrationBuilder.DropTable(
                name: "PlanFeatures");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
