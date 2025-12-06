using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PalmMap.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedRandomUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var hasher = new PasswordHasher<object>();
            var random = new Random(42); // Seed for reproducibility
            
            var users = new[]
            {
                "Alex", "Marina", "Pavel", "Elena", "Dmitry", "Irina", 
                "Sergey", "Natalia", "Viktor", "Olga", "Andrey", "Svetlana"
            };

            for (int i = 0; i < 12; i++)
            {
                var userId = Guid.NewGuid().ToString();
                var userName = users[i].ToLower();
                var email = $"{userName}{i}@example.com";
                var hashedPassword = hasher.HashPassword(null, "Password123!");
                var points = random.Next(10, 500);
                var level = (points / 100) + 1;

                migrationBuilder.InsertData(
                    table: "AspNetUsers",
                    columns: new[] { "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", 
                                   "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                                   "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
                                   "LockoutEnd", "AccessFailedCount", "DisplayName", "Level", "Points", "ReviewCount", "AvatarUrl" },
                    values: new object[] { userId, email, email.ToUpper(), email, email.ToUpper(), 
                                          true, hashedPassword, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                                          null, false, false, false,
                                          null, 0, users[i], level, points, random.Next(0, 15), null }
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Email",
                keyValues: new object[] { "alex0@example.com", "marina1@example.com", "pavel2@example.com", 
                                         "elena3@example.com", "dmitry4@example.com", "irina5@example.com",
                                         "sergey6@example.com", "natalia7@example.com", "viktor8@example.com",
                                         "olga9@example.com", "andrey10@example.com", "svetlana11@example.com" }
            );
        }
    }
}
