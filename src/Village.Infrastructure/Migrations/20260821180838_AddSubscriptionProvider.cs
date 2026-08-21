using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppStoreOriginalTransactionId",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePlayPurchaseToken",
                table: "Families",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionProvider",
                table: "Families",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppStoreOriginalTransactionId",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "GooglePlayPurchaseToken",
                table: "Families");

            migrationBuilder.DropColumn(
                name: "SubscriptionProvider",
                table: "Families");
        }
    }
}
