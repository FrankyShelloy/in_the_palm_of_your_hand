namespace PalmMap.Api.Dtos;

public record CreateReviewRequest(string Content);
public record ReviewResponse(Guid Id, string Content, DateTime CreatedAt);
