using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTrialReminderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TrialReminderSentAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrialReminderSentAt",
                table: "Tenants");
        }
    }
}
