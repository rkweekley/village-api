using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreSubtasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentChoreId",
                table: "Chores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chores_ParentChoreId",
                table: "Chores",
                column: "ParentChoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chores_Chores_ParentChoreId",
                table: "Chores",
                column: "ParentChoreId",
                principalTable: "Chores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chores_Chores_ParentChoreId",
                table: "Chores");

            migrationBuilder.DropIndex(
                name: "IX_Chores_ParentChoreId",
                table: "Chores");

            migrationBuilder.DropColumn(
                name: "ParentChoreId",
                table: "Chores");
        }
    }
}
