using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplacePlanGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPublicListings",
                table: "Plans",
                type: "int",
                nullable: true);

            // Seed the new fields on the canonical plan IDs from the original
            // AddPlansAndFeatures migration: 1=Homestead, 2=Farm, 3=Dairy.
            // Idempotent on second run since these are simple UPDATEs and the
            // feature INSERTs are guarded by NOT EXISTS — safe even if an
            // admin manually toggled flags before this migration.
            migrationBuilder.Sql(@"
                UPDATE [Plans] SET [MaxPublicListings] = 3    WHERE [Id] = 1; -- Homestead
                UPDATE [Plans] SET [MaxPublicListings] = NULL WHERE [Id] = 2; -- Farm (unlimited)
                UPDATE [Plans] SET [MaxPublicListings] = NULL WHERE [Id] = 3; -- Dairy (unlimited)
            ");

            // AppFeature enum values: 21 = MarketplaceMapPin, 22 = StripeDeposits.
            // Paid plans (Farm, Dairy) get both. Homestead gets neither.
            migrationBuilder.Sql(@"
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT 2, 21, 1 WHERE NOT EXISTS (SELECT 1 FROM [PlanFeatures] WHERE PlanId = 2 AND Feature = 21);
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT 2, 22, 1 WHERE NOT EXISTS (SELECT 1 FROM [PlanFeatures] WHERE PlanId = 2 AND Feature = 22);
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT 3, 21, 1 WHERE NOT EXISTS (SELECT 1 FROM [PlanFeatures] WHERE PlanId = 3 AND Feature = 21);
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT 3, 22, 1 WHERE NOT EXISTS (SELECT 1 FROM [PlanFeatures] WHERE PlanId = 3 AND Feature = 22);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPublicListings",
                table: "Plans");
        }
    }
}
