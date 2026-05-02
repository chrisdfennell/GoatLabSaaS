using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoCapAndCustomBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicAccentColor",
                table: "Tenants",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicWelcomeMessage",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPhotosPerGoat",
                table: "Plans",
                type: "int",
                nullable: true);

            // Seed photo caps and CustomBranding feature on the canonical
            // plan IDs (1=Homestead, 2=Farm, 3=Dairy). Photo cap is the
            // upgrade trigger that hits sellers in the moment they're
            // building a polished listing — Homestead 5, Farm 20, Dairy
            // unlimited. CustomBranding is Dairy-only and visible on the
            // public farm page.
            migrationBuilder.Sql(@"
                UPDATE [Plans] SET [MaxPhotosPerGoat] = 5    WHERE [Id] = 1; -- Homestead
                UPDATE [Plans] SET [MaxPhotosPerGoat] = 20   WHERE [Id] = 2; -- Farm
                UPDATE [Plans] SET [MaxPhotosPerGoat] = NULL WHERE [Id] = 3; -- Dairy (unlimited)
            ");

            // AppFeature.CustomBranding = 23. Dairy only.
            migrationBuilder.Sql(@"
                INSERT INTO [PlanFeatures] (PlanId, Feature, Enabled)
                SELECT 3, 23, 1 WHERE NOT EXISTS (SELECT 1 FROM [PlanFeatures] WHERE PlanId = 3 AND Feature = 23);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicAccentColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PublicWelcomeMessage",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MaxPhotosPerGoat",
                table: "Plans");
        }
    }
}
