using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Models;

namespace PalmMap.Api.Services;

public class AchievementService
{
    private readonly ApplicationDbContext _db;

    public AchievementService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AwardAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var earnedIds = await _db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .Select(ua => ua.AchievementId)
            .ToListAsync(ct);

        var toAward = await _db.Achievements
            .Where(a => a.RequiredReviews <= user.ReviewCount && !earnedIds.Contains(a.Id))
            .ToListAsync(ct);

        if (toAward.Count == 0)
        {
            return;
        }

        foreach (var achievement in toAward)
        {
            _db.UserAchievements.Add(new UserAchievement
            {
                UserId = user.Id,
                AchievementId = achievement.Id,
                EarnedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
