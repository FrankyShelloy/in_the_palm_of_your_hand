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
                Code = "first-steps",
                Title = "Первые шаги",
                Description = "Начало пути картографа здоровья",
                Icon = "👣",
                ProgressType = AchievementProgressType.FirstPlaceAdded,
                TargetValue = 1,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Code = "attentive-citizen",
                Title = "Внимательный горожанин",
                Description = "Проявил внимание к городской среде",
                Icon = "👁️",
                ProgressType = AchievementProgressType.ReviewsCount,
                TargetValue = 10,
                RequiredReviews = 10
            },
            new Achievement
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Code = "health-photographer",
                Title = "Фотограф здоровья",
                Description = "Визуально документируешь городскую среду",
                Icon = "📸",
                ProgressType = AchievementProgressType.PhotosCount,
                TargetValue = 15,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Code = "objective-critic",
                Title = "Объективный критик",
                Description = "Помогаешь другим сделать осознанный выбор",
                Icon = "✍️",
                ProgressType = AchievementProgressType.DetailedReviewsCount,
                TargetValue = 5,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Code = "balanced-opinions",
                Title = "Баланс мнений",
                Description = "Сбалансированный взгляд на городскую инфраструктуру",
                Icon = "⚖️",
                ProgressType = AchievementProgressType.BalancedReviews,
                TargetValue = 2,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Code = "infrastructure-detective",
                Title = "Детектив инфраструктуры",
                Description = "Помогаешь расширять картографию города",
                Icon = "🔍",
                ProgressType = AchievementProgressType.NewPlacesAdded,
                TargetValue = 3,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Code = "health-expert",
                Title = "Эксперт здоровья",
                Description = "Стал настоящим гидом по здоровому образу жизни в городе",
                Icon = "🏆",
                ProgressType = AchievementProgressType.HighRatedHealthyPlaces,
                TargetValue = 10,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Code = "platform-legend",
                Title = "Легенда платформы",
                Description = "Признанное сообществом лицо проекта",
                Icon = "👑",
                ProgressType = AchievementProgressType.TopThreeRating,
                TargetValue = 1,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Code = "team-player",
                Title = "Командный игрок",
                Description = "Твои находки полезны сообществу",
                Icon = "🤝",
                ProgressType = AchievementProgressType.PlacesReviewedByOthers,
                TargetValue = 5,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Code = "emotional-analyst",
                Title = "Эмоциональный аналитик",
                Description = "Умеешь различать нюансы качества",
                Icon = "💭",
                ProgressType = AchievementProgressType.AllRatingsUsed,
                TargetValue = 5,
                RequiredReviews = 0
            },
            new Achievement
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Code = "fast-fingers",
                Title = "Быстрые пальцы",
                Description = "Активный день исследователя",
                Icon = "⚡",
                ProgressType = AchievementProgressType.PlacesInOneDay,
                TargetValue = 3,
                RequiredReviews = 0
            });
    }
}
