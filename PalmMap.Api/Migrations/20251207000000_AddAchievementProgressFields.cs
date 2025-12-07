using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalmMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Achievements",
                type: "TEXT",
                nullable: false,
                defaultValue: "🏆");

            migrationBuilder.AddColumn<int>(
                name: "ProgressType",
                table: "Achievements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetValue",
                table: "Achievements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "ProgressType",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "Achievements");
        }
    }
}



