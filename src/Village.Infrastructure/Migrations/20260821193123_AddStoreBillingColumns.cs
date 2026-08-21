using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreBillingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppStoreAppAccountToken",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppStoreEnvironment",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewEnabled",
                table: "Families",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePlaySubscriptionId",
                table: "Families",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppStoreAppAccountToken",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "AppStoreEnvironment",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "AutoRenewEnabled",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "GooglePlaySubscriptionId",
                table: "Families");
        }
    }
}
