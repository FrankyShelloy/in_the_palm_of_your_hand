using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalmMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedNewAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Обновляем существующие достижения напрямую через SQL
            // Используем UPDATE для обновления существующих записей
            migrationBuilder.Sql(@"
                UPDATE Achievements 
                SET Code = 'first-steps',
                    Title = 'Первые шаги',
                    Description = 'Начало пути картографа здоровья',
                    Icon = '👣',
                    ProgressType = 1,
                    TargetValue = 1,
                    RequiredReviews = 0
                WHERE Id = '11111111-1111-1111-1111-111111111111';
            ");

            migrationBuilder.Sql(@"
                UPDATE Achievements 
                SET Code = 'attentive-citizen',
                    Title = 'Внимательный горожанин',
                    Description = 'Проявил внимание к городской среде',
                    Icon = '👁️',
                    ProgressType = 2,
                    TargetValue = 10,
                    RequiredReviews = 10
                WHERE Id = '22222222-2222-2222-2222-222222222222';
            ");

            migrationBuilder.Sql(@"
                UPDATE Achievements 
                SET Code = 'health-photographer',
                    Title = 'Фотограф здоровья',
                    Description = 'Визуально документируешь городскую среду',
                    Icon = '📸',
                    ProgressType = 3,
                    TargetValue = 15,
                    RequiredReviews = 0
                WHERE Id = '33333333-3333-3333-3333-333333333333';
            ");

            // Если достижения не существуют, добавляем их (INSERT OR IGNORE для SQLite)
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO Achievements (Id, Code, Title, Description, Icon, ProgressType, TargetValue, RequiredReviews)
                VALUES 
                    ('11111111-1111-1111-1111-111111111111', 'first-steps', 'Первые шаги', 'Начало пути картографа здоровья', '👣', 1, 1, 0),
                    ('22222222-2222-2222-2222-222222222222', 'attentive-citizen', 'Внимательный горожанин', 'Проявил внимание к городской среде', '👁️', 2, 10, 10),
                    ('33333333-3333-3333-3333-333333333333', 'health-photographer', 'Фотограф здоровья', 'Визуально документируешь городскую среду', '📸', 3, 15, 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Удаляем новые достижения
            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

            // Восстанавливаем старые
            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Code", "Description", "Icon", "ProgressType", "RequiredReviews", "TargetValue", "Title" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "first-review", "Оставьте первый отзыв", "🏆", 0, 1, 0, "Первый отзыв" });

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Code", "Description", "Icon", "ProgressType", "RequiredReviews", "TargetValue", "Title" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), "five-reviews", "Оставьте 5 отзывов", "🏆", 0, 5, 0, "Рецензент" });

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Code", "Description", "Icon", "ProgressType", "RequiredReviews", "TargetValue", "Title" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333333"), "ten-reviews", "Оставьте 10 отзывов", "🏆", 0, 10, 0, "Рассказчик" });
        }
    }
}

