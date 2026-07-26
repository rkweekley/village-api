using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Village.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SchoolAndMealPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    WeekEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlans_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealPlans_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Ingredients = table.Column<string>(type: "TEXT", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    PrepTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Servings = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PhotoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsFamilyFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    MealType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanEntries_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealPlanEntries_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SchoolWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AssignedToId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PointsPossible = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubmissionNote = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PointsEarned = table.Column<int>(type: "INTEGER", nullable: true),
                    GradedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolWorks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolWorks_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolWorks_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolWorks_Users_AssignedById",
                        column: x => x.AssignedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolWorks_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SchoolWorks_Users_GradedById",
                        column: x => x.GradedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MealVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MealPlanEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Preference = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealVotes_MealPlanEntries_MealPlanEntryId",
                        column: x => x.MealPlanEntryId,
                        principalTable: "MealPlanEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealVotes_Users_FamilyMemberId",
                        column: x => x.FamilyMemberId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_MealPlanId_DayOfWeek_MealType",
                table: "MealPlanEntries",
                columns: new[] { "MealPlanId", "DayOfWeek", "MealType" });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanEntries_RecipeId",
                table: "MealPlanEntries",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_CreatedById",
                table: "MealPlans",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_FamilyId_WeekStart",
                table: "MealPlans",
                columns: new[] { "FamilyId", "WeekStart" });

            migrationBuilder.CreateIndex(
                name: "IX_MealVotes_FamilyMemberId",
                table: "MealVotes",
                column: "FamilyMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MealVotes_MealPlanEntryId_FamilyMemberId",
                table: "MealVotes",
                columns: new[] { "MealPlanEntryId", "FamilyMemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_CreatedById",
                table: "Recipes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_FamilyId_IsActive",
                table: "Recipes",
                columns: new[] { "FamilyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_FamilyId_IsFamilyFavorite",
                table: "Recipes",
                columns: new[] { "FamilyId", "IsFamilyFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolWorks_AssignedById",
                table: "SchoolWorks",
                column: "AssignedById");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolWorks_AssignedToId",
                table: "SchoolWorks",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolWorks_FamilyId_DueDate_Status",
                table: "SchoolWorks",
                columns: new[] { "FamilyId", "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolWorks_GradedById",
                table: "SchoolWorks",
                column: "GradedById");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolWorks_SubjectId",
                table: "SchoolWorks",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_FamilyId_IsActive",
                table: "Subjects",
                columns: new[] { "FamilyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_FamilyId_SortOrder",
                table: "Subjects",
                columns: new[] { "FamilyId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealVotes");

            migrationBuilder.DropTable(
                name: "SchoolWorks");

            migrationBuilder.DropTable(
                name: "MealPlanEntries");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "MealPlans");

            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
