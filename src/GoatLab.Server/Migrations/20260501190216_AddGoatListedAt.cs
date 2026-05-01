using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGoatListedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ListedAt",
                table: "Goats",
                type: "datetime2",
                nullable: true);

            // Backfill: existing listed goats get their CreatedAt as a stand-in
            // ListedAt so the freshness badge has a sensible date instead of
            // showing nothing. Unlisted goats stay null.
            migrationBuilder.Sql(@"
                UPDATE [Goats]
                SET    [ListedAt] = [CreatedAt]
                WHERE  [IsListedForSale] = 1
                  AND  [ListedAt] IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListedAt",
                table: "Goats");
        }
    }
}
