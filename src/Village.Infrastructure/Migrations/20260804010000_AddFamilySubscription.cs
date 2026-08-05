using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilySubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionExpiresAt",
                table: "Families",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Families",
                type: "text",
                nullable: false,
                defaultValue: "trial");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionTier",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Families",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 8, 18, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StripeCustomerId", table: "Families");
            migrationBuilder.DropColumn(name: "StripeSubscriptionId", table: "Families");
            migrationBuilder.DropColumn(name: "SubscriptionExpiresAt", table: "Families");
            migrationBuilder.DropColumn(name: "SubscriptionStatus", table: "Families");
            migrationBuilder.DropColumn(name: "SubscriptionTier", table: "Families");
            migrationBuilder.DropColumn(name: "TrialEndsAt", table: "Families");
        }
    }
}
