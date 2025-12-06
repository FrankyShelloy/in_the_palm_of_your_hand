using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Dtos;
using PalmMap.Api.Models;

namespace PalmMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var achievements = await _db.UserAchievements
            .Where(ua => ua.UserId == user.Id)
            .Include(ua => ua.Achievement)
            .Select(ua => new
            {
                ua.Achievement.Code,
                ua.Achievement.Title,
                ua.Achievement.Description,
                ua.Achievement.RequiredReviews,
                ua.EarnedAt
            })
            .ToListAsync();

        return Ok(new
        {
            user.Email,
            user.DisplayName,
            user.Level,
            user.ReviewCount,
            user.Points,
            user.IsAdmin,
            Achievements = achievements
        });
    }

    // Получить рейтинг: топ-10 пользователей и позицию текущего пользователя
    [HttpGet("ratings")]
    public async Task<IActionResult> GetRatings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        // Получить всех пользователей, отсортировано по очкам (убывание)
        var allUsers = await _db.Users
            .OrderByDescending(u => u.Points)
            .ThenByDescending(u => u.Level)
            .ToListAsync();

        // Топ-10
        var top10 = allUsers.Take(10)
            .Select((u, idx) => new UserRatingEntry(idx + 1, u.Id, u.DisplayName ?? "Аноним", u.Points, u.Level))
            .ToList();

        // Позиция текущего пользователя (1-indexed)
        var userPosition = allUsers.FindIndex(u => u.Id == user.Id) + 1;

        // Текущий пользователь (если не в топ-10)
        var currentUserRating = new UserRatingEntry(userPosition, user.Id, user.DisplayName ?? "Аноним", user.Points, user.Level);

        return Ok(new UserRatingsResponse(top10, userPosition, currentUserRating));
    }
}
