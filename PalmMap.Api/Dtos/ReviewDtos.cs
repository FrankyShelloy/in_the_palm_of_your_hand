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
    int UserVote
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
    [property: JsonPropertyName("userVote")] int UserVote // 1 = like, -1 = dislike, 0 = none
);

public record UpdateReviewRequest(
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("comment")] string? Comment
);

public record VoteRequest(
    [property: JsonPropertyName("isLike")] bool IsLike
);
