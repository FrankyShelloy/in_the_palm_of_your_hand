using System.Text.Json.Serialization;

namespace PalmMap.Api.Dtos;

public record CreateReviewRequest(
    [property: JsonPropertyName("content")] string Content
);
public record ReviewResponse(Guid Id, string Content, DateTime CreatedAt);
