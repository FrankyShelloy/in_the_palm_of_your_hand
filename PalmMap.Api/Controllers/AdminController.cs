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
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// Проверка, является ли текущий пользователь администратором
    /// </summary>
    private async Task<ApplicationUser?> GetAdminUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsAdmin)
            return null;
        return user;
    }

    /// <summary>
    /// Проверить, является ли текущий пользователь админом
    /// GET /api/admin/check
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckAdmin()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        return Ok(new { isAdmin = user.IsAdmin });
    }

    /// <summary>
    /// Получить статистику модерации
    /// GET /api/admin/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var pending = await _db.Reviews.CountAsync(r => r.ModerationStatus == ModerationStatus.Pending);
        var approved = await _db.Reviews.CountAsync(r => r.ModerationStatus == ModerationStatus.Approved);
        var rejected = await _db.Reviews.CountAsync(r => r.ModerationStatus == ModerationStatus.Rejected);

        return Ok(new ModerationStatsResponse(pending, approved, rejected, pending + approved + rejected));
    }

    /// <summary>
    /// Получить отзывы на модерации
    /// GET /api/admin/reviews?status=pending&page=1&pageSize=20
    /// </summary>
    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews(
        [FromQuery] string status = "pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var query = _db.Reviews.Include(r => r.User).AsQueryable();

        // Фильтрация по статусу
        query = status.ToLower() switch
        {
            "pending" => query.Where(r => r.ModerationStatus == ModerationStatus.Pending),
            "approved" => query.Where(r => r.ModerationStatus == ModerationStatus.Approved),
            "rejected" => query.Where(r => r.ModerationStatus == ModerationStatus.Rejected),
            "all" => query,
            _ => query.Where(r => r.ModerationStatus == ModerationStatus.Pending)
        };

        var total = await query.CountAsync();

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ModerationReviewResponse(
                r.Id,
                r.UserId,
                r.User.DisplayName ?? r.User.UserName ?? "Аноним",
                r.PlaceId,
                r.PlaceName,
                r.Rating,
                r.Comment,
                r.PhotoPath != null ? $"/uploads/reviews/{r.PhotoPath}" : null,
                r.CreatedAt,
                r.ModerationStatus.ToString().ToLower()
            ))
            .ToListAsync();

        return Ok(new
        {
            reviews,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Модерировать отзыв (одобрить/отклонить)
    /// POST /api/admin/reviews/{id}/moderate
    /// </summary>
    [HttpPost("reviews/{id}/moderate")]
    public async Task<IActionResult> ModerateReview(Guid id, [FromBody] ModerateRequest request)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var review = await _db.Reviews.FindAsync(id);
        if (review == null)
            return NotFound(new { message = "Отзыв не найден" });

        switch (request.Action.ToLower())
        {
            case "approve":
                review.ModerationStatus = ModerationStatus.Approved;
                review.RejectionReason = null;
                break;
            case "reject":
                review.ModerationStatus = ModerationStatus.Rejected;
                review.RejectionReason = request.Reason;
                break;
            default:
                return BadRequest(new { message = "Неверное действие. Используйте 'approve' или 'reject'" });
        }

        review.ModeratedAt = DateTime.UtcNow;
        review.ModeratorId = admin.Id;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = request.Action.ToLower() == "approve" ? "Отзыв одобрен" : "Отзыв отклонён",
            reviewId = id,
            status = review.ModerationStatus.ToString().ToLower()
        });
    }

    /// <summary>
    /// Массовое одобрение отзывов
    /// POST /api/admin/reviews/approve-all
    /// </summary>
    [HttpPost("reviews/approve-all")]
    public async Task<IActionResult> ApproveAll([FromBody] List<Guid> ids)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var reviews = await _db.Reviews
            .Where(r => ids.Contains(r.Id) && r.ModerationStatus == ModerationStatus.Pending)
            .ToListAsync();

        foreach (var review in reviews)
        {
            review.ModerationStatus = ModerationStatus.Approved;
            review.ModeratedAt = DateTime.UtcNow;
            review.ModeratorId = admin.Id;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Одобрено {reviews.Count} отзывов" });
    }

    /// <summary>
    /// Удалить отзыв (только для админа)
    /// DELETE /api/admin/reviews/{id}
    /// </summary>
    [HttpDelete("reviews/{id}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var review = await _db.Reviews.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
        if (review == null)
            return NotFound(new { message = "Отзыв не найден" });

        // Уменьшаем счётчик отзывов у пользователя
        if (review.User != null)
        {
            review.User.ReviewCount = Math.Max(0, review.User.ReviewCount - 1);
        }

        // Удаляем фото если есть
        if (!string.IsNullOrEmpty(review.PhotoPath))
        {
            try
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
                var photoPath = Path.Combine(uploadsDir, review.PhotoPath);
                if (System.IO.File.Exists(photoPath))
                {
                    System.IO.File.Delete(photoPath);
                }
            }
            catch { /* Игнорируем ошибки удаления файла */ }
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Отзыв удалён" });
    }

    /// <summary>
    /// Назначить пользователя администратором
    /// POST /api/admin/users/{userId}/make-admin
    /// </summary>
    [HttpPost("users/{userId}/make-admin")]
    public async Task<IActionResult> MakeAdmin(string userId)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        user.IsAdmin = true;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = $"Пользователь {user.DisplayName ?? user.UserName} назначен администратором" });
    }

    /// <summary>
    /// Снять права администратора
    /// POST /api/admin/users/{userId}/remove-admin
    /// </summary>
    [HttpPost("users/{userId}/remove-admin")]
    public async Task<IActionResult> RemoveAdmin(string userId)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        // Нельзя снять права с себя
        if (admin.Id == userId)
            return BadRequest(new { message = "Нельзя снять права администратора с себя" });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        user.IsAdmin = false;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = $"Права администратора сняты с {user.DisplayName ?? user.UserName}" });
    }

    /// <summary>
    /// Получить список всех пользователей
    /// GET /api/admin/users?page=1&pageSize=20
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var admin = await GetAdminUser();
        if (admin == null)
            return Forbid();

        var total = await _db.Users.CountAsync();

        var users = await _db.Users
            .OrderByDescending(u => u.Points)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                id = u.Id,
                userName = u.UserName,
                displayName = u.DisplayName,
                email = u.Email,
                level = u.Level,
                points = u.Points,
                reviewCount = u.ReviewCount,
                isAdmin = u.IsAdmin,
                emailConfirmed = u.EmailConfirmed
            })
            .ToListAsync();

        return Ok(new
        {
            users,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
}
