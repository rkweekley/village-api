using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilySubscriptionCanceled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionCanceledAt",
                table: "Families",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionCanceledByUserId",
                table: "Families",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SubscriptionCanceledAt", table: "Families");
            migrationBuilder.DropColumn(name: "SubscriptionCanceledByUserId", table: "Families");
        }
    }
}
