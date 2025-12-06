using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PalmMap.Api.Dtos;
using PalmMap.Api.Models;
using PalmMap.Api.Services;

namespace PalmMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailSenderDev _emailSender;

    private readonly string _frontendUrl;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration, IEmailSenderDev emailSender, IHttpClientFactory httpClientFactory)
    {
        _userManager = userManager;
        _configuration = configuration;
        _emailSender = emailSender;
        _httpClientFactory = httpClientFactory;
        _frontendUrl = configuration.GetValue<string>("FrontendUrl") ?? "http://localhost";
    }

    // Начать VK OAuth с PKCE: перенаправляет пользователя на страницу авторизации VK ID
    [HttpGet("vk/login")]
    public IActionResult VkLogin()
    {
        var vk = _configuration.GetSection("Vk");
        var clientId = vk["ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) return BadRequest(new { message = "VK ClientId not configured" });

        var redirectUri = $"{_frontendUrl}/api/auth/vk/callback";
        
        // Генерация PKCE параметров
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");
        
        // Сохраняем code_verifier в cookie для использования в callback
        Response.Cookies.Append("vk_code_verifier", codeVerifier, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // для localhost
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10)
        });
        Response.Cookies.Append("vk_state", state, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10)
        });

        // VK ID OAuth 2.1 URL
        var authorize = $"https://id.vk.com/authorize?" +
            $"response_type=code" +
            $"&client_id={WebUtility.UrlEncode(clientId)}" +
            $"&redirect_uri={WebUtility.UrlEncode(redirectUri)}" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&scope=vkid.personal_info%20email";
            
        return Redirect(authorize);
    }

    // Callback endpoint VK redirects to with `code`
    [HttpGet("vk/callback")]
    public async Task<IActionResult> VkCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? device_id)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Code is required" });

        // Проверка state
        var savedState = Request.Cookies["vk_state"];
        if (string.IsNullOrWhiteSpace(savedState) || savedState != state)
        {
            return BadRequest(new { message = "Invalid state parameter" });
        }

        var codeVerifier = Request.Cookies["vk_code_verifier"];
        if (string.IsNullOrWhiteSpace(codeVerifier))
        {
            return BadRequest(new { message = "Code verifier not found" });
        }

        // Удаляем cookies
        Response.Cookies.Delete("vk_code_verifier");
        Response.Cookies.Delete("vk_state");

        var vk = _configuration.GetSection("Vk");
        var clientId = vk["ClientId"];
        var clientSecret = vk["ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return BadRequest(new { message = "VK client credentials not configured" });

        var redirectUri = $"{_frontendUrl}/api/auth/vk/callback";

        var http = _httpClientFactory.CreateClient();
        
        // Exchange code for access token через VK ID API
        var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["device_id"] = device_id ?? Guid.NewGuid().ToString()
        });

        var tokenRes = await http.PostAsync("https://id.vk.com/oauth2/auth", tokenRequestContent);
        var tokenJson = await tokenRes.Content.ReadAsStringAsync();
        
        if (!tokenRes.IsSuccessStatusCode) 
        {
            return BadRequest(new { message = "Failed to exchange code for VK token", details = tokenJson });
        }

        using var doc = System.Text.Json.JsonDocument.Parse(tokenJson);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("access_token", out var accessTokenEl))
        {
            return BadRequest(new { message = "No access_token in response", details = tokenJson });
        }
        
        var accessToken = accessTokenEl.GetString();
        var vkUserId = root.TryGetProperty("user_id", out var userIdEl) ? userIdEl.GetInt64() : 0;

        // Получаем данные пользователя из id_token или через API
        string? email = null;
        string? firstName = null;
        string? lastName = null;
        string? photo = null;

        // Попробуем получить данные из id_token (JWT)
        if (root.TryGetProperty("id_token", out var idTokenEl))
        {
            var idToken = idTokenEl.GetString();
            if (!string.IsNullOrEmpty(idToken))
            {
                var payload = DecodeJwtPayload(idToken);
                if (payload.HasValue)
                {
                    var p = payload.Value;
                    email = p.TryGetProperty("email", out var e) ? e.GetString() : null;
                    firstName = p.TryGetProperty("given_name", out var fn) ? fn.GetString() : null;
                    lastName = p.TryGetProperty("family_name", out var ln) ? ln.GetString() : null;
                    photo = p.TryGetProperty("picture", out var pic) ? pic.GetString() : null;
                    if (vkUserId == 0 && p.TryGetProperty("sub", out var sub))
                    {
                        long.TryParse(sub.GetString(), out vkUserId);
                    }
                }
            }
        }

        // Если не получили данные из id_token, попробуем через VK API
        if (string.IsNullOrEmpty(firstName) && vkUserId > 0)
        {
            var userInfoUrl = $"https://api.vk.com/method/users.get?user_ids={vkUserId}&fields=first_name,last_name,photo_200&access_token={accessToken}&v=5.131";
            var infoRes = await http.GetAsync(userInfoUrl);
            if (infoRes.IsSuccessStatusCode)
            {
                var infoJson = await infoRes.Content.ReadAsStringAsync();
                using var infoDoc = System.Text.Json.JsonDocument.Parse(infoJson);
                if (infoDoc.RootElement.TryGetProperty("response", out var respArr) && respArr.GetArrayLength() > 0)
                {
                    var response = respArr[0];
                    firstName = response.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                    lastName = response.TryGetProperty("last_name", out var ln) ? ln.GetString() : null;
                    photo = response.TryGetProperty("photo_200", out var ph) ? ph.GetString() : null;
                }
            }
        }

        // Find or create user
        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            user = await _userManager.FindByEmailAsync(email);
        }

        if (user == null && vkUserId > 0)
        {
            // Try by username vk_{id}@vk.local (full username format)
            var userName = $"vk_{vkUserId}@vk.local";
            user = await _userManager.FindByNameAsync(userName);
        }

        if (user == null)
        {
            var displayName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(displayName)) displayName = $"VK User {vkUserId}";
            
            var newUser = new ApplicationUser
            {
                UserName = !string.IsNullOrWhiteSpace(email) ? email : $"vk_{vkUserId}@vk.local",
                Email = !string.IsNullOrWhiteSpace(email) ? email : null,
                DisplayName = displayName,
                EmailConfirmed = true
            };
            var createRes = await _userManager.CreateAsync(newUser);
            if (!createRes.Succeeded)
            {
                return BadRequest(createRes.Errors);
            }
            user = newUser;
        }

        // Update avatar if available
        if (!string.IsNullOrWhiteSpace(photo))
        {
            user.AvatarUrl = photo;
            await _userManager.UpdateAsync(user);
        }

        // Generate JWT and redirect to frontend with token
        var jwt = GenerateJwt(user);
        var redirect = _frontendUrl + $"/?vk_token={WebUtility.UrlEncode(jwt)}";
        return Redirect(redirect);
    }

    // PKCE helpers
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static System.Text.Json.JsonElement? DecodeJwtPayload(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1];
            // Add padding
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
             return BadRequest(new { message = "Имя пользователя обязательно" });
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            return Conflict(new { message = "Пользователь с таким email уже существует" });
        }

        var nameExists = await _userManager.Users.AnyAsync(u => u.DisplayName == request.DisplayName);
        if (nameExists)
        {
            return Conflict(new { message = "Пользователь с таким именем уже существует" });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Level = 1,
            ReviewCount = 0
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        // generate email confirmation token and send confirmation link
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedUserId = WebUtility.UrlEncode(user.Id);
        var confirmLink = $"{_frontendUrl}/confirm-email?userId={encodedUserId}&token={encodedToken}";

        var html = $"<p>Здравствуйте {WebUtility.HtmlEncode(user.DisplayName ?? user.Email)}!</p>" +
                   $"<p>Спасибо за регистрацию на НаЛадони. Подтвердите почту, перейдя по ссылке:</p>" +
                   $"<p><a href=\"{confirmLink}\">Подтвердить почту</a></p>";

        await _emailSender.SendEmailAsync(user.Email!, "Подтверждение регистрации — НаЛадони", html);

        return Accepted(new { message = "Пользователь создан. Проверьте почту для подтверждения." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized();
        }

        var valid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!valid)
        {
            return Unauthorized();
        }

        if (!user.EmailConfirmed)
        {
            return BadRequest(new { message = "Email не подтвержден. Проверьте почту и подтвердите email." });
        }

        var token = GenerateJwt(user);
        return Ok(new AuthResponse(token, user.Email!, user.DisplayName));
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // ASP.NET Core автоматически декодирует query-параметры
        var res = await _userManager.ConfirmEmailAsync(user, token);
        if (!res.Succeeded) return BadRequest(res.Errors);

        return Ok(new { message = "Email подтверждён" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) 
        {
            return BadRequest(new { message = "Пользователь с таким email не зарегистрирован" });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebUtility.UrlEncode(token);
        var resetLink = $"{_frontendUrl}/reset-password?userId={WebUtility.UrlEncode(user.Id)}&token={encoded}";

        var html = $"<p>Здравствуйте!</p><p>Чтобы сбросить пароль, перейдите по ссылке:</p><p><a href=\"{resetLink}\">Сбросить пароль</a></p>";
        await _emailSender.SendEmailAsync(user.Email!, "Сброс пароля — НаЛадони", html);

        return Ok(new { message = "Письмо для сброса пароля отправлено" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(req.UserId);
        if (user == null) return NotFound();

        // Токен приходит уже декодированным из JSON body
        var res = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!res.Succeeded) return BadRequest(res.Errors);

        // Auto-confirm email after password reset
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        return Ok(new { message = "Пароль успешно сброшен" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.Level,
            user.ReviewCount,
            user.Points,
            user.AvatarUrl
        });
    }

    private string GenerateJwt(ApplicationUser user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "dev_secret_key_change_me"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
