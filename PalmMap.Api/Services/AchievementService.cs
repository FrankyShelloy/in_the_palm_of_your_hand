using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Models;

namespace PalmMap.Api.Services;

public class AchievementProgressResult
{
    public List<Guid> NewlyEarned { get; set; } = new();
    public Dictionary<Guid, int> Progress { get; set; } = new(); // AchievementId -> Progress percentage
}

public class AchievementService
{
    private readonly ApplicationDbContext _db;

    public AchievementService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AchievementProgressResult> CheckAndAwardAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var result = new AchievementProgressResult();
        
        // Получаем все достижения
        var allAchievements = await _db.Achievements.ToListAsync(ct);
        
        // Получаем уже заработанные достижения
        var earnedIds = await _db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .Select(ua => ua.AchievementId)
            .ToListAsync(ct);

        // Для каждого достижения проверяем прогресс
        foreach (var achievement in allAchievements)
        {
            if (earnedIds.Contains(achievement.Id))
            {
                // Уже получено - прогресс 100%
                result.Progress[achievement.Id] = 100;
                continue;
            }

            int currentValue = 0;
            bool isCompleted = false;

            switch (achievement.ProgressType)
            {
                case AchievementProgressType.FirstPlaceAdded:
                    // Проверяем, есть ли хотя бы одно место, созданное пользователем
                    var placesCount = await _db.Places
                        .CountAsync(p => p.CreatedByUserId == user.Id, ct);
                    currentValue = placesCount > 0 ? 1 : 0;
                    isCompleted = placesCount > 0;
                    break;

                case AchievementProgressType.ReviewsCount:
                    // Количество уникальных объектов, которые пользователь оценил
                    var uniquePlacesReviewed = await _db.Reviews
                        .Where(r => r.UserId == user.Id && r.ModerationStatus == ModerationStatus.Approved)
                        .Select(r => r.PlaceId)
                        .Distinct()
                        .CountAsync(ct);
                    currentValue = uniquePlacesReviewed;
                    isCompleted = uniquePlacesReviewed >= achievement.TargetValue;
                    break;

                case AchievementProgressType.PhotosCount:
                    // Количество отзывов с фотографиями
                    var photosCount = await _db.Reviews
                        .CountAsync(r => r.UserId == user.Id && r.PhotoPath != null && r.ModerationStatus == ModerationStatus.Approved, ct);
                    currentValue = photosCount;
                    isCompleted = photosCount >= achievement.TargetValue;
                    break;

                case AchievementProgressType.DetailedReviewsCount:
                    // Количество развёрнутых отзывов (более 100 символов)
                    var detailedReviews = await _db.Reviews
                        .CountAsync(r => r.UserId == user.Id && 
                                       r.Comment != null && 
                                       r.Comment.Length > 100 && 
                                       r.ModerationStatus == ModerationStatus.Approved, ct);
                    currentValue = detailedReviews;
                    isCompleted = detailedReviews >= achievement.TargetValue;
                    break;

                case AchievementProgressType.BalancedReviews:
                    // Оценить по 2 объекта каждого типа (healthy_food, gym, alcohol/pharmacy)
                    var userReviews = await _db.Reviews
                        .Where(r => r.UserId == user.Id && r.ModerationStatus == ModerationStatus.Approved)
                        .Select(r => r.PlaceId)
                        .Distinct()
                        .ToListAsync(ct);
                    
                    var healthyFoodPlaces = await _db.Places
                        .Where(p => userReviews.Contains(p.Id.ToString()) && p.Type == "healthy_food")
                        .Select(p => p.Id)
                        .Distinct()
                        .CountAsync(ct);
                    
                    var gymPlaces = await _db.Places
                        .Where(p => userReviews.Contains(p.Id.ToString()) && p.Type == "gym")
                        .Select(p => p.Id)
                        .Distinct()
                        .CountAsync(ct);
                    
                    var shopPlaces = await _db.Places
                        .Where(p => userReviews.Contains(p.Id.ToString()) && (p.Type == "pharmacy" || p.Type == "alcohol"))
                        .Select(p => p.Id)
                        .Distinct()
                        .CountAsync(ct);
                    
                    var minCount = Math.Min(Math.Min(healthyFoodPlaces, gymPlaces), shopPlaces);
                    currentValue = minCount;
                    isCompleted = healthyFoodPlaces >= 2 && gymPlaces >= 2 && shopPlaces >= 2;
                    break;

                case AchievementProgressType.NewPlacesAdded:
                    // Добавить 3 объекта, которых ещё нет на карте (новые объекты пользователя)
                    var newPlacesCount = await _db.Places
                        .CountAsync(p => p.CreatedByUserId == user.Id, ct);
                    currentValue = newPlacesCount;
                    isCompleted = newPlacesCount >= achievement.TargetValue;
                    break;

                case AchievementProgressType.HighRatedHealthyPlaces:
                    // 10 объектов здорового питания с средним рейтингом 4.5+
                    var healthyFoodPlaceIds = await _db.Places
                        .Where(p => p.Type == "healthy_food")
                        .Select(p => p.Id.ToString())
                        .ToListAsync(ct);
                    
                    var highRatedHealthyPlaces = await _db.Reviews
                        .Where(r => healthyFoodPlaceIds.Contains(r.PlaceId) && 
                                   r.ModerationStatus == ModerationStatus.Approved)
                        .GroupBy(r => r.PlaceId)
                        .Select(g => new { PlaceId = g.Key, AvgRating = g.Average(r => r.Rating) })
                        .Where(x => x.AvgRating >= 4.5)
                        .Select(x => x.PlaceId)
                        .Distinct()
                        .CountAsync(ct);
                    
                    currentValue = highRatedHealthyPlaces;
                    isCompleted = highRatedHealthyPlaces >= achievement.TargetValue;
                    break;

                case AchievementProgressType.TopThreeRating:
                    // Топ-3 в рейтинге пользователей
                    var allUsers = await _db.Users
                        .OrderByDescending(u => u.Points)
                        .ThenByDescending(u => u.Level)
                        .Select(u => u.Id)
                        .ToListAsync(ct);
                    var userPosition = allUsers.IndexOf(user.Id) + 1;
                    currentValue = userPosition <= 3 ? 1 : 0;
                    isCompleted = userPosition <= 3;
                    break;

                case AchievementProgressType.PlacesReviewedByOthers:
                    // 5 объектов пользователя, которые оценили другие
                    var userPlaces = await _db.Places
                        .Where(p => p.CreatedByUserId == user.Id)
                        .Select(p => p.Id.ToString())
                        .ToListAsync(ct);
                    
                    var reviewedPlaces = await _db.Reviews
                        .Where(r => userPlaces.Contains(r.PlaceId) && 
                                   r.UserId != user.Id && 
                                   r.ModerationStatus == ModerationStatus.Approved)
                        .Select(r => r.PlaceId)
                        .Distinct()
                        .CountAsync(ct);
                    currentValue = reviewedPlaces;
                    isCompleted = reviewedPlaces >= achievement.TargetValue;
                    break;

                case AchievementProgressType.AllRatingsUsed:
                    // Использовать все оценки от 1 до 5
                    var usedRatings = await _db.Reviews
                        .Where(r => r.UserId == user.Id && r.ModerationStatus == ModerationStatus.Approved)
                        .Select(r => r.Rating)
                        .Distinct()
                        .ToListAsync(ct);
                    currentValue = usedRatings.Count;
                    isCompleted = usedRatings.Contains(1) && usedRatings.Contains(2) && 
                                 usedRatings.Contains(3) && usedRatings.Contains(4) && 
                                 usedRatings.Contains(5);
                    break;

                case AchievementProgressType.PlacesInOneDay:
                    // 3 объекта за один день
                    var today = DateTime.UtcNow.Date;
                    var placesToday = await _db.Places
                        .CountAsync(p => p.CreatedByUserId == user.Id && 
                                        p.CreatedAt.Date == today, ct);
                    currentValue = placesToday;
                    isCompleted = placesToday >= achievement.TargetValue;
                    break;
            }

            // Вычисляем процент прогресса
            int progressPercent = achievement.TargetValue > 0 
                ? Math.Min(100, (int)((double)currentValue / achievement.TargetValue * 100))
                : 0;
            
            result.Progress[achievement.Id] = progressPercent;

            // Если достижение выполнено, награждаем
            if (isCompleted)
            {
                _db.UserAchievements.Add(new UserAchievement
                {
                    UserId = user.Id,
                    AchievementId = achievement.Id,
                    EarnedAt = DateTime.UtcNow
                });
                result.NewlyEarned.Add(achievement.Id);
            }
        }

        if (result.NewlyEarned.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return result;
    }

    // Старый метод для обратной совместимости
    public async Task AwardAsync(ApplicationUser user, CancellationToken ct = default)
    {
        await CheckAndAwardAsync(user, ct);
    }
}
