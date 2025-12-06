using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
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
            Achievements = achievements
        });
    }
}
