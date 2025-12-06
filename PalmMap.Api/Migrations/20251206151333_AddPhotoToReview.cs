using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalmMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoToReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Reviews",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Reviews");
        }
    }
}
