using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Tenants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Tenants");
        }
    }
}
