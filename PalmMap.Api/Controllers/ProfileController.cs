using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Dtos;
using PalmMap.Api.Models;
using PalmMap.Api.Services;

namespace PalmMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly AchievementService _achievementService;

    public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, AchievementService achievementService)
    {
        _userManager = userManager;
        _db = db;
        _achievementService = achievementService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var progressResult = await _achievementService.CheckAndAwardAsync(user);

        var allAchievements = await _db.Achievements
            .OrderBy(a => a.ProgressType)
            .ThenBy(a => a.TargetValue)
            .ToListAsync();

        var earnedAchievementIds = await _db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .Select(ua => ua.AchievementId)
            .ToListAsync();

        var earnedAchievementsWithDates = await _db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.EarnedAt);

        var achievementsWithProgress = allAchievements.Select(a =>
        {
            var earned = earnedAchievementIds.Contains(a.Id);
            var earnedAt = earned ? earnedAchievementsWithDates.GetValueOrDefault(a.Id) : (DateTime?)null;

            return new
            {
                a.Id,
                a.Code,
                a.Title,
                a.Description,
                a.Icon,
                a.ProgressType,
                a.TargetValue,
                Earned = earned,
                EarnedAt = earnedAt,
                Progress = progressResult.Progress.GetValueOrDefault(a.Id, 0),
                IsNewlyEarned = progressResult.NewlyEarned.Contains(a.Id)
            };
        }).ToList();

        return Ok(new
        {
            user.Email,
            user.DisplayName,
            user.Level,
            user.ReviewCount,
            user.Points,
            user.IsAdmin,
            Achievements = achievementsWithProgress,
            NewlyEarnedAchievements = progressResult.NewlyEarned.Select(id => 
                achievementsWithProgress.First(a => a.Id == id)
            ).ToList()
        });
    }

    [HttpGet("ratings")]
    public async Task<IActionResult> GetRatings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var allUsers = await _db.Users
            .OrderByDescending(u => u.Points)
            .ThenByDescending(u => u.Level)
            .ToListAsync();

        var top10 = allUsers.Take(10)
            .Select((u, idx) => new UserRatingEntry(idx + 1, u.Id, u.DisplayName ?? "Аноним", u.Points, u.Level))
            .ToList();

        var userPosition = allUsers.FindIndex(u => u.Id == user.Id) + 1;

        var currentUserRating = new UserRatingEntry(userPosition, user.Id, user.DisplayName ?? "Аноним", user.Points, user.Level);

        return Ok(new UserRatingsResponse(top10, userPosition, currentUserRating));
    }
}
