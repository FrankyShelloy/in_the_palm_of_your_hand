using System.Text.Json.Serialization;

namespace PalmMap.Api.Dtos;

public record CreateReviewRequest(
    [property: JsonPropertyName("placeId")] string PlaceId,
    [property: JsonPropertyName("placeName")] string PlaceName,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("comment")] string? Comment
);

public record ReviewResponse(
    Guid Id, 
    string PlaceId, 
    string PlaceName, 
    int Rating, 
    string? Comment, 
    string? PhotoUrl,
    DateTime CreatedAt,
    int Likes,
    int Dislikes,
    int UserVote,
    [property: JsonPropertyName("moderationStatus")] string ModerationStatus,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason = null
);

// Для отображения отзывов на объекте карты (без привязки к пользователю)
public record PlaceReviewResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("userLevel")] int UserLevel,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("photoUrl")] string? PhotoUrl,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("likes")] int Likes,
    [property: JsonPropertyName("dislikes")] int Dislikes,
    [property: JsonPropertyName("userVote")] int UserVote, // 1 = like, -1 = dislike, 0 = none
    [property: JsonPropertyName("moderationStatus")] string ModerationStatus = "approved"
);

public record UpdateReviewRequest(
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("comment")] string? Comment,
    // При обновлении можно пометить, что существующее фото следует удалить
    [property: JsonPropertyName("deletePhoto")] bool DeletePhoto = false
);

public record VoteRequest(
    [property: JsonPropertyName("isLike")] bool IsLike
);

// DTO для рейтинга пользователей
public record UserRatingEntry(
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("level")] int Level
);

public record UserRatingsResponse(
    [property: JsonPropertyName("top10")] List<UserRatingEntry> Top10,
    [property: JsonPropertyName("currentUserPosition")] int CurrentUserPosition,
    [property: JsonPropertyName("currentUser")] UserRatingEntry CurrentUser
);

// DTO для модерации
public record ModerationReviewResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("placeId")] string PlaceId,
    [property: JsonPropertyName("placeName")] string PlaceName,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("photoUrl")] string? PhotoUrl,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("moderationStatus")] string ModerationStatus
);

public record ModerateRequest(
    [property: JsonPropertyName("action")] string Action, // "approve" или "reject"
    [property: JsonPropertyName("reason")] string? Reason = null
);

public record ModerationStatsResponse(
    [property: JsonPropertyName("pending")] int Pending,
    [property: JsonPropertyName("approved")] int Approved,
    [property: JsonPropertyName("rejected")] int Rejected,
    [property: JsonPropertyName("total")] int Total
);

