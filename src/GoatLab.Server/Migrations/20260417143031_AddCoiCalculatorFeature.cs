using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCoiCalculatorFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill PlanFeature row for AppFeature.CoiCalculator (=17) across the
            // three seeded plans (1=Homestead, 2=Farm, 3=Dairy). Coi is a paid-tier
            // upsell — Homestead off, Farm/Dairy on. Guarded so it's a no-op on
            // databases where those plans were renamed or removed.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Plans] WHERE Id IN (1,2,3))
                BEGIN
                    INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                    SELECT v.PlanId, v.Feature, v.Enabled
                    FROM (VALUES
                        (1, 17, CAST(0 AS bit)),  -- Homestead: CoiCalculator off
                        (2, 17, CAST(1 AS bit)),  -- Farm: CoiCalculator on
                        (3, 17, CAST(1 AS bit))   -- Dairy: CoiCalculator on
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
            migrationBuilder.Sql("DELETE FROM [PlanFeatures] WHERE Feature = 17;");
        }
    }
}
