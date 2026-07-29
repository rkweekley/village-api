using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Chores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chores_CreatedById",
                table: "Chores",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Chores_Users_CreatedById",
                table: "Chores",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chores_Users_CreatedById",
                table: "Chores");

            migrationBuilder.DropIndex(
                name: "IX_Chores_CreatedById",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Chores");
        }
    }
}
