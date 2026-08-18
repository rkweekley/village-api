using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignForeignKeyDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_OrganizerId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_Users_AssignedToId",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_Users_CompletedById",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Users_CreatedById",
                table: "MealPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_MealVotes_Users_FamilyMemberId",
                table: "MealVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PointsTransactions_Users_UserId",
                table: "PointsTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_CreatedById",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Users_UserId",
                table: "RewardRedemptions");

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_OrganizerId",
                table: "CalendarEvents",
                column: "OrganizerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_Users_AssignedToId",
                table: "ChoreAssignments",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_Users_CompletedById",
                table: "ChoreCompletions",
                column: "CompletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Users_CreatedById",
                table: "MealPlans",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MealVotes_Users_FamilyMemberId",
                table: "MealVotes",
                column: "FamilyMemberId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointsTransactions_Users_UserId",
                table: "PointsTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_CreatedById",
                table: "Recipes",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Users_UserId",
                table: "RewardRedemptions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Users_UserId",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_CreatedById",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_PointsTransactions_Users_UserId",
                table: "PointsTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_MealVotes_Users_FamilyMemberId",
                table: "MealVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Users_CreatedById",
                table: "MealPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_Users_CompletedById",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_Users_AssignedToId",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_OrganizerId",
                table: "CalendarEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Users_UserId",
                table: "RewardRedemptions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_CreatedById",
                table: "Recipes",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PointsTransactions_Users_UserId",
                table: "PointsTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealVotes_Users_FamilyMemberId",
                table: "MealVotes",
                column: "FamilyMemberId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Users_CreatedById",
                table: "MealPlans",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_Users_CompletedById",
                table: "ChoreCompletions",
                column: "CompletedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_Users_AssignedToId",
                table: "ChoreAssignments",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_OrganizerId",
                table: "CalendarEvents",
                column: "OrganizerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
