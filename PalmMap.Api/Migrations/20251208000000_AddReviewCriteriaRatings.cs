using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalmMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewCriteriaRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CriteriaRatings",
                table: "Reviews",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDirectRating",
                table: "Reviews",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriteriaRatings",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsDirectRating",
                table: "Reviews");
        }
    }
}
