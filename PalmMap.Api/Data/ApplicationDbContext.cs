using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Models;

namespace PalmMap.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewVote> ReviewVotes => Set<ReviewVote>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Achievement>()
            .HasIndex(a => a.Code)
            .IsUnique();

        builder.Entity<UserAchievement>()
            .HasKey(ua => new { ua.UserId, ua.AchievementId });

        builder.Entity<UserAchievement>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAchievements)
            .HasForeignKey(ua => ua.UserId);

        builder.Entity<UserAchievement>()
            .HasOne(ua => ua.Achievement)
            .WithMany(a => a.UserAchievements)
            .HasForeignKey(ua => ua.AchievementId);

        builder.Entity<ReviewVote>()
            .HasIndex(rv => new { rv.ReviewId, rv.UserId })
            .IsUnique();

        builder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId);

        builder.Entity<Achievement>().HasData(
            new Achievement
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "first-review",
                Title = "Первый отзыв",
                Description = "Оставьте первый отзыв",
                RequiredReviews = 1
            },
            new Achievement
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Code = "five-reviews",
                Title = "Рецензент",
                Description = "Оставьте 5 отзывов",
                RequiredReviews = 5
            },
            new Achievement
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Code = "ten-reviews",
                Title = "Рассказчик",
                Description = "Оставьте 10 отзывов",
                RequiredReviews = 10
            });
    }
}
