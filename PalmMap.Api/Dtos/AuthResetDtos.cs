using System.Text.Json.Serialization;

namespace PalmMap.Api.Dtos;

public record ResetPasswordRequest(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("newPassword")] string NewPassword
);

public record ForgotPasswordRequest(
    [property: JsonPropertyName("email")] string Email
);
