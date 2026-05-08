using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanFeatureGapsAndNewGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two changes here, both PlanFeature data only — no schema change.
            //
            // 1. Fix gaps in the Homestead (PlanId=1) seed:
            //    - DataExport (13): the FAQ explicitly promises "no lock-in,
            //      export to CSV any time". Homestead users couldn't.
            //    - BuyerWaitlist (19): Homestead can list goats publicly but
            //      had no in-app inbox for interested buyers — broken funnel.
            //
            // 2. Seed three new feature gates that previously rode along free
            //    or as part of CoiCalculator. INSERTs are idempotent (NOT EXISTS
            //    guard) so re-running on a partially-migrated DB is safe.
            //    - VetShareLinks (24): Farm + Dairy
            //    - GoatTransfers (25): Farm + Dairy
            //    - MateRecommendations (26): seeded on for any plan that
            //      already has CoiCalculator (17). Preserves existing access
            //      for tenants who upgraded to a tier with CoiCalculator on.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Plans] WHERE Id IN (1,2,3))
                BEGIN
                    -- Homestead gaps
                    INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                    SELECT v.PlanId, v.Feature, v.Enabled
                    FROM (VALUES
                        (1, 13, CAST(1 AS bit)),  -- Homestead: DataExport on
                        (1, 19, CAST(1 AS bit))   -- Homestead: BuyerWaitlist on
                    ) v(PlanId, Feature, Enabled)
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [PlanFeatures] pf
                        WHERE pf.PlanId = v.PlanId AND pf.Feature = v.Feature
                    );

                    -- New gates: VetShareLinks (24), GoatTransfers (25), Farm+Dairy
                    INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                    SELECT v.PlanId, v.Feature, v.Enabled
                    FROM (VALUES
                        (1, 24, CAST(0 AS bit)),  -- Homestead: VetShareLinks off
                        (2, 24, CAST(1 AS bit)),  -- Farm: VetShareLinks on
                        (3, 24, CAST(1 AS bit)),  -- Dairy: VetShareLinks on
                        (1, 25, CAST(0 AS bit)),  -- Homestead: GoatTransfers off
                        (2, 25, CAST(1 AS bit)),  -- Farm: GoatTransfers on
                        (3, 25, CAST(1 AS bit))   -- Dairy: GoatTransfers on
                    ) v(PlanId, Feature, Enabled)
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [PlanFeatures] pf
                        WHERE pf.PlanId = v.PlanId AND pf.Feature = v.Feature
                    );
                END
            ");

            // MateRecommendations (26) seeded on for every plan that has
            // CoiCalculator (17) enabled. Future-proof against custom plans
            // by joining on PlanFeatures rather than hardcoding plan ids.
            migrationBuilder.Sql(@"
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT pf.PlanId, 26, pf.Enabled
                FROM [PlanFeatures] pf
                WHERE pf.Feature = 17
                  AND NOT EXISTS (
                      SELECT 1 FROM [PlanFeatures] x
                      WHERE x.PlanId = pf.PlanId AND x.Feature = 26
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the three new feature rows. Leaves the Homestead DataExport
            // and BuyerWaitlist seeds in place — those are arguably bug fixes
            // we wouldn't want to roll back to broken state.
            migrationBuilder.Sql("DELETE FROM [PlanFeatures] WHERE Feature IN (24, 25, 26);");
        }
    }
}
