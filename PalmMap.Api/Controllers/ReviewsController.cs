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

    // Получить все отзывы текущего пользователя (для профиля)
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

            return new ReviewResponse(
                r.Id, 
                r.PlaceId, 
                r.PlaceName, 
                r.Rating, 
                r.Comment, 
                r.CreatedAt,
                r.Votes.Count(v => v.IsLike),
                r.Votes.Count(v => !v.IsLike),
                userVote
            );
        });

        return Ok(response);
    }

    // Получить все отзывы для конкретного места (для карты, без авторизации)
    [HttpGet("place/{placeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByPlace(string placeId)
    {
        var currentUserId = _userManager.GetUserId(User);

        var reviews = await _db.Reviews
            .Where(r => r.PlaceId == placeId)
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

            return new PlaceReviewResponse(
                r.Id,
                r.UserId,
                r.User.DisplayName ?? "Аноним",
                r.User.Level,
                r.Rating,
                r.Comment,
                r.CreatedAt,
                r.Votes.Count(v => v.IsLike),
                r.Votes.Count(v => !v.IsLike),
                userVote
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

        if (request.Rating < 1 || request.Rating > 5)
        {
            return BadRequest(new { message = "Рейтинг должен быть от 1 до 5" });
        }

        review.Rating = request.Rating;
        review.Comment = request.Comment;
        
        await _db.SaveChangesAsync();
        // При обновлении голоса не меняются, но нам нужно вернуть полный объект
        // Загрузим голоса для корректного ответа или вернем 0/0 если лениво (но лучше загрузить)
        // В данном случае просто вернем 0, так как UI обновится отдельным запросом или сохранит старые значения
        // Но лучше сделать правильно
        var likes = await _db.ReviewVotes.CountAsync(v => v.ReviewId == review.Id && v.IsLike);
        var dislikes = await _db.ReviewVotes.CountAsync(v => v.ReviewId == review.Id && !v.IsLike);
        // UserVote для автора всегда 0 (он не может голосовать за себя? или может? в коде может)
        // Проверим голос автора
        var userVoteObj = await _db.ReviewVotes.FirstOrDefaultAsync(v => v.ReviewId == review.Id && v.UserId == user.Id);
        int userVote = userVoteObj != null ? (userVoteObj.IsLike ? 1 : -1) : 0;

        return Ok(new ReviewResponse(review.Id, review.PlaceId, review.PlaceName, review.Rating, review.Comment, review.CreatedAt, likes, dislikes, userVote));
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
        
        // Decrement user review count
        user.ReviewCount = Math.Max(0, user.ReviewCount - 1);
        // Recalculate level (simplified logic, ideally should be in service)
        // For now just keep it simple or call service if needed. 
        // Assuming level only goes up for now or stays.
        
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var review = await _db.Reviews.Include(r => r.Votes).FirstOrDefaultAsync(r => r.Id == id);
        if (review == null) return NotFound();

        var existingVote = review.Votes.FirstOrDefault(v => v.UserId == user.Id);

        if (existingVote != null)
        {
            if (existingVote.IsLike == request.IsLike)
            {
                // Toggle off (remove vote)
                _db.ReviewVotes.Remove(existingVote);
            }
            else
            {
                // Change vote
                existingVote.IsLike = request.IsLike;
            }
        }
        else
        {
            // New vote
            var vote = new ReviewVote
            {
                ReviewId = id,
                UserId = user.Id,
                IsLike = request.IsLike
            };
            _db.ReviewVotes.Add(vote);
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    // Проверить, оставлял ли пользователь отзыв на это место
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

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceId))
        {
            return BadRequest(new { message = "PlaceId обязателен" });
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            return BadRequest(new { message = "Рейтинг должен быть от 1 до 5" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        // Проверка: один отзыв на одно место от одного пользователя
        var existingReview = await _db.Reviews
            .FirstOrDefaultAsync(r => r.UserId == user.Id && r.PlaceId == request.PlaceId);

        if (existingReview != null)
        {
            return Conflict(new { message = "Вы уже оставили отзыв на этот объект" });
        }

        var review = new Review
        {
            UserId = user.Id,
            PlaceId = request.PlaceId,
            PlaceName = request.PlaceName ?? "Объект",
            Rating = request.Rating,
            Comment = request.Comment?.Trim()
        };

        _db.Reviews.Add(review);

        user.ReviewCount += 1;
        user.Level = Math.Max(1, 1 + (user.ReviewCount / 5));

        await _db.SaveChangesAsync();
        await _achievementService.AwardAsync(user);

        return CreatedAtAction(nameof(Get), new { id = review.Id }, 
            new ReviewResponse(review.Id, review.PlaceId, review.PlaceName, review.Rating, review.Comment, review.CreatedAt, 0, 0, 0));
    }
}
