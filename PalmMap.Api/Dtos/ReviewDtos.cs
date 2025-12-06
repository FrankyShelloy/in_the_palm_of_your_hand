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
    DateTime CreatedAt
);

// Для отображения отзывов на объекте карты (без привязки к пользователю)
public record PlaceReviewResponse(
    string UserName,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);
