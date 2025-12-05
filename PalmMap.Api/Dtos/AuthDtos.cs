using System.Text.Json.Serialization;

namespace PalmMap.Api.Dtos;

public record RegisterRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("displayName")] string DisplayName
);

public record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password
);

public record AuthResponse(string Token, string Email, string? DisplayName);
