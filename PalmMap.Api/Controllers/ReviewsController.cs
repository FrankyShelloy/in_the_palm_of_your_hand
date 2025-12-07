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
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AchievementService _achievementService;

    public ReviewsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AchievementService achievementService)
    {
        _db = db;
        _userManager = userManager;
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

        var reviews = await _db.Reviews
            .Where(r => r.UserId == user.Id)
            .Include(r => r.Votes)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var response = reviews.Select(r => {
            var userVoteObj = r.Votes.FirstOrDefault(v => v.UserId == user.Id);
            int userVote = userVoteObj != null ? (userVoteObj.IsLike ? 1 : -1) : 0;

            Dictionary<string, int>? criteriaRatings = null;
            if (!string.IsNullOrEmpty(r.CriteriaRatings))
            {
                try
                {
                    criteriaRatings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(r.CriteriaRatings);
                }
                catch { }
            }

            return new ReviewResponse(
                r.Id, 
                r.PlaceId, 
                r.PlaceName, 
                r.Rating,
                criteriaRatings,
                r.IsDirectRating,
                r.Comment, 
                r.PhotoPath != null ? $"/uploads/reviews/{r.PhotoPath}" : null,
                r.CreatedAt,
                r.Votes.Count(v => v.IsLike),
                r.Votes.Count(v => !v.IsLike),
                userVote,
                r.ModerationStatus.ToString().ToLower(),
                r.RejectionReason
            );
        });

        return Ok(response);
    }

    [HttpGet("place/{placeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPlace(string placeId)
    {
        var currentUserId = _userManager.GetUserId(User);

        var reviews = await _db.Reviews
            .Where(r => r.PlaceId == placeId && r.ModerationStatus == ModerationStatus.Approved)
            .OrderByDescending(r => r.CreatedAt)
            .Include(r => r.User)
            .Include(r => r.Votes)
            .ToListAsync();

        var response = reviews.Select(r => {
            var userVoteObj = currentUserId != null ? r.Votes.FirstOrDefault(v => v.UserId == currentUserId) : null;
            int userVote = 0;
            if (userVoteObj != null)
            {
                userVote = userVoteObj.IsLike ? 1 : -1;
            }

            Dictionary<string, int>? criteriaRatings = null;
            if (!string.IsNullOrEmpty(r.CriteriaRatings))
            {
                try
                {
                    criteriaRatings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(r.CriteriaRatings);
                }
                catch { }
            }

            return new PlaceReviewResponse(
                r.Id,
                r.UserId,
                r.User.DisplayName ?? "Аноним",
                r.User.Level,
                r.Rating,
                criteriaRatings,
                r.IsDirectRating,
                r.Comment,
                r.PhotoPath != null ? $"/uploads/reviews/{r.PhotoPath}" : null,
                r.CreatedAt,
                r.Votes.Count(v => v.IsLike),
                r.Votes.Count(v => !v.IsLike),
                userVote,
                r.ModerationStatus.ToString().ToLower()
            );
        });

        return Ok(response);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        if (review.UserId != user.Id)
        {
            return Forbid();
        }

        int finalRating;
        bool isDirectRating;
        string? criteriaRatingsJson = null;

        if (request.CriteriaRatings != null && request.CriteriaRatings.Any())
        {
            if (request.CriteriaRatings.Count != 4)
            {
                return BadRequest(new { message = "Должно быть указано 4 критерия" });
            }

            foreach (var kvp in request.CriteriaRatings)
            {
                if (kvp.Value < 1 || kvp.Value > 5)
                {
                    return BadRequest(new { message = $"Оценка критерия '{kvp.Key}' должна быть от 1 до 5" });
                }
            }

            finalRating = (int)Math.Round(request.CriteriaRatings.Values.Average());
            isDirectRating = false;
            criteriaRatingsJson = System.Text.Json.JsonSerializer.Serialize(request.CriteriaRatings);
        }
        else if (request.Rating.HasValue)
        {
            if (request.Rating.Value < 1 || request.Rating.Value > 5)
            {
                return BadRequest(new { message = "Рейтинг должен быть от 1 до 5" });
            }
            finalRating = request.Rating.Value;
            isDirectRating = true;
        }
        else
        {
            return BadRequest(new { message = "Необходимо указать либо общий рейтинг, либо оценки по критериям" });
        }

        if (request.Comment != null && request.Comment.Length > 2000)
        {
            return BadRequest(new { message = "Комментарий не должен превышать 2000 символов" });
        }

        review.Rating = finalRating;
        review.CriteriaRatings = criteriaRatingsJson;
        review.IsDirectRating = isDirectRating;
        review.Comment = request.Comment;

        if (request.DeletePhoto && !string.IsNullOrEmpty(review.PhotoPath))
        {
            try
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
                var oldPath = Path.Combine(uploadsDir, review.PhotoPath);
                if (System.IO.File.Exists(oldPath))
                {
                    System.IO.File.Delete(oldPath);
                }
            }
            catch
            {
            }

            review.PhotoPath = null;
        }

        await _db.SaveChangesAsync();
        var likes = await _db.ReviewVotes.CountAsync(v => v.ReviewId == review.Id && v.IsLike);
        var dislikes = await _db.ReviewVotes.CountAsync(v => v.ReviewId == review.Id && !v.IsLike);
        var userVoteObj = await _db.ReviewVotes.FirstOrDefaultAsync(v => v.ReviewId == review.Id && v.UserId == user.Id);
        int userVote = userVoteObj != null ? (userVoteObj.IsLike ? 1 : -1) : 0;

        Dictionary<string, int>? criteriaRatingsDict = null;
        if (!string.IsNullOrEmpty(review.CriteriaRatings))
        {
            try
            {
                criteriaRatingsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(review.CriteriaRatings);
            }
            catch { }
        }

        return Ok(new ReviewResponse(review.Id, review.PlaceId, review.PlaceName, review.Rating, criteriaRatingsDict, review.IsDirectRating, review.Comment, 
            review.PhotoPath != null ? $"/uploads/reviews/{review.PhotoPath}" : null, 
            review.CreatedAt, likes, dislikes, userVote,
            review.ModerationStatus.ToString().ToLower(),
            review.RejectionReason));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        if (review.UserId != user.Id)
        {
            return Forbid();
        }

        _db.Reviews.Remove(review);
        
        user.ReviewCount = Math.Max(0, user.ReviewCount - 1);
        user.Points = Math.Max(0, user.Points - 10); // -10 очков за удалённый отзыв
        user.Level = Math.Max(1, 1 + (user.ReviewCount / 5)); // Пересчёт уровня
        
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var review = await _db.Reviews.Include(r => r.Votes).FirstOrDefaultAsync(r => r.Id == id);
            if (review == null) return NotFound();

            var existingVote = await _db.ReviewVotes
                .FirstOrDefaultAsync(v => v.ReviewId == id && v.UserId == user.Id);

            if (existingVote != null)
            {
                if (existingVote.IsLike == request.IsLike)
                {
                    _db.ReviewVotes.Remove(existingVote);
                }
                else
                {
                    existingVote.IsLike = request.IsLike;
                }
            }
            else
            {
                var vote = new ReviewVote
                {
                    ReviewId = id,
                    UserId = user.Id,
                    IsLike = request.IsLike
                };
                _db.ReviewVotes.Add(vote);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Ok();
        }
    }

    [HttpGet("check/{placeId}")]
    public async Task<IActionResult> CheckReview(string placeId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var exists = await _db.Reviews.AnyAsync(r => r.UserId == user.Id && r.PlaceId == placeId);
        return Ok(new { hasReview = exists });
    }

    [HttpGet("criteria/{placeType}")]
    [AllowAnonymous]
    public IActionResult GetCriteria(string placeType)
    {
        var criteria = ReviewCriteria.GetCriteriaForType(placeType);
        return Ok(criteria);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceId))
        {
            return BadRequest(new { message = "PlaceId обязателен" });
        }

        int finalRating;
        bool isDirectRating;
        string? criteriaRatingsJson = null;
        Dictionary<string, int>? criteriaRatingsDict = null;

        if (request.CriteriaRatings != null && request.CriteriaRatings.Any())
        {
            if (request.CriteriaRatings.Count != 4)
            {
                return BadRequest(new { message = "Должно быть указано 4 критерия" });
            }

            foreach (var kvp in request.CriteriaRatings)
            {
                if (kvp.Value < 1 || kvp.Value > 5)
                {
                    return BadRequest(new { message = $"Оценка критерия '{kvp.Key}' должна быть от 1 до 5" });
                }
            }

            finalRating = (int)Math.Round(request.CriteriaRatings.Values.Average());
            isDirectRating = false;
            criteriaRatingsJson = System.Text.Json.JsonSerializer.Serialize(request.CriteriaRatings);
            criteriaRatingsDict = request.CriteriaRatings;
        }
        else if (request.Rating.HasValue)
        {
            if (request.Rating.Value < 1 || request.Rating.Value > 5)
            {
                return BadRequest(new { message = "Рейтинг должен быть от 1 до 5" });
            }
            finalRating = request.Rating.Value;
            isDirectRating = true;
        }
        else
        {
            return BadRequest(new { message = "Необходимо указать либо общий рейтинг, либо оценки по критериям" });
        }

        if (request.Comment != null && request.Comment.Length > 2000)
        {
            return BadRequest(new { message = "Комментарий не должен превышать 2000 символов" });
        }

        if (request.PlaceName != null && request.PlaceName.Length > 200)
        {
            return BadRequest(new { message = "Название места слишком длинное" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser == null)
        {
            return Unauthorized();
        }

        var existingReview = await _db.Reviews
            .FirstOrDefaultAsync(r => r.UserId == dbUser.Id && r.PlaceId == request.PlaceId);

        if (existingReview != null)
        {
            return Conflict(new { message = "Вы уже оставили отзыв на этот объект" });
        }

        var review = new Review
        {
            UserId = dbUser.Id,
            PlaceId = request.PlaceId,
            PlaceName = request.PlaceName ?? "Объект",
            Rating = finalRating,
            CriteriaRatings = criteriaRatingsJson,
            IsDirectRating = isDirectRating,
            Comment = request.Comment?.Trim(),
            ModerationStatus = ModerationStatus.Pending // Новые отзывы отправляются на модерацию
        };

        _db.Reviews.Add(review);

        dbUser.ReviewCount += 1;
        dbUser.Points += 10; // +10 очков за каждый отзыв
        dbUser.Level = Math.Max(1, 1 + (dbUser.ReviewCount / 5));

        await _db.SaveChangesAsync();
        
        await _achievementService.CheckAndAwardAsync(dbUser);

        return CreatedAtAction(nameof(Get), new { id = review.Id }, 
            new ReviewResponse(review.Id, review.PlaceId, review.PlaceName, review.Rating, criteriaRatingsDict, isDirectRating, review.Comment, null, review.CreatedAt, 0, 0, 0, "pending"));
    }

    [HttpPost("{reviewId}/photo")]
    public async Task<IActionResult> UploadPhoto(Guid reviewId, IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
        {
            return BadRequest(new { message = "Файл не выбран" });
        }

        if (photo.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { message = "Размер файла не должен превышать 5MB" });
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowedTypes.Contains(photo.ContentType.ToLower()))
        {
            return BadRequest(new { message = "Допустимые форматы: JPEG, PNG, WebP, GIF" });
        }

        var magicBytesValid = await ValidateImageMagicBytes(photo);
        if (!magicBytesValid)
        {
            return BadRequest(new { message = "Недопустимый формат файла. Загрузите настоящее изображение." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == user.Id);
        if (review == null)
        {
            return NotFound(new { message = "Отзыв не найден" });
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
        Directory.CreateDirectory(uploadsDir);

        var originalExtension = Path.GetExtension(photo.FileName);
        var safeExtension = originalExtension?.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".png" => ".png",
            ".webp" => ".webp",
            ".gif" => ".gif",
            _ => ".jpg" // fallback
        };
        var fileName = $"{reviewId}_{DateTime.UtcNow:yyyyMMddHHmmss}{safeExtension}";
        var filePath = Path.Combine(uploadsDir, fileName);
        
        var fullPath = Path.GetFullPath(filePath);
        if (!fullPath.StartsWith(Path.GetFullPath(uploadsDir)))
        {
            return BadRequest(new { message = "Недопустимое имя файла" });
        }

        if (!string.IsNullOrEmpty(review.PhotoPath))
        {
            var oldPath = Path.Combine(uploadsDir, review.PhotoPath);
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        review.PhotoPath = fileName;
        await _db.SaveChangesAsync();

        await _achievementService.CheckAndAwardAsync(user);

        return Ok(new { photoUrl = $"/uploads/reviews/{fileName}" });
    }

    private static async Task<bool> ValidateImageMagicBytes(IFormFile file)
    {
        var signatures = new Dictionary<string, byte[][]>
        {
            { "jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { "png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { "gif", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } } },
            { "webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } } // RIFF header, WebP has WEBP at offset 8
        };

        var maxHeaderLength = signatures.Values.SelectMany(s => s).Max(s => s.Length);
        var header = new byte[maxHeaderLength];

        using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header, 0, maxHeaderLength);

        foreach (var format in signatures.Values)
        {
            foreach (var signature in format)
            {
                if (header.Take(signature.Length).SequenceEqual(signature))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
