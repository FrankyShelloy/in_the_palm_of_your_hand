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
        _frontendUrl = configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5014";
    }

    // Начать VK OAuth: перенаправляет пользователя на страницу авторизации VK
    [HttpGet("vk/login")]
    public IActionResult VkLogin()
    {
        var vk = _configuration.GetSection("Vk");
        var clientId = vk["ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) return BadRequest(new { message = "VK ClientId not configured" });

        var redirectUri = Url.ActionLink(nameof(VkCallback), "Auth", null, Request.Scheme);
        var state = Guid.NewGuid().ToString("N");
        // You may want to persist state to validate CSRF; omitted for brevity

        var authorize = $"https://oauth.vk.com/authorize?client_id={WebUtility.UrlEncode(clientId)}&display=page&redirect_uri={WebUtility.UrlEncode(redirectUri)}&scope=email&response_type=code&v={vk["ApiVersion"]}&state={state}";
        return Redirect(authorize);
    }

    // Callback endpoint VK redirects to with `code`
    [HttpGet("vk/callback")]
    public async Task<IActionResult> VkCallback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Code is required" });

        var vk = _configuration.GetSection("Vk");
        var clientId = vk["ClientId"];
        var clientSecret = vk["ClientSecret"];
        var apiVersion = vk["ApiVersion"] ?? "5.131";
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return BadRequest(new { message = "VK client credentials not configured" });

        var redirectUri = Url.ActionLink(nameof(VkCallback), "Auth", null, Request.Scheme);

        var http = _httpClientFactory.CreateClient();
        // Exchange code for access token
        var tokenUrl = $"https://oauth.vk.com/access_token?client_id={clientId}&client_secret={clientSecret}&redirect_uri={WebUtility.UrlEncode(redirectUri)}&code={WebUtility.UrlEncode(code)}";
        var tokenRes = await http.GetAsync(tokenUrl);
        if (!tokenRes.IsSuccessStatusCode) return BadRequest(new { message = "Failed to exchange code for VK token" });

        var tokenJson = await tokenRes.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(tokenJson);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        string? email = null;
        if (root.TryGetProperty("email", out var emailEl)) email = emailEl.GetString();
        var vkUserId = root.GetProperty("user_id").GetInt64();

        // Fetch user info
        var userInfoUrl = $"https://api.vk.com/method/users.get?user_ids={vkUserId}&fields=first_name,last_name,photo_200&access_token={accessToken}&v={apiVersion}";
        var infoRes = await http.GetAsync(userInfoUrl);
        if (!infoRes.IsSuccessStatusCode) return BadRequest(new { message = "Failed to get VK user info" });
        var infoJson = await infoRes.Content.ReadAsStringAsync();
        using var infoDoc = System.Text.Json.JsonDocument.Parse(infoJson);
        var response = infoDoc.RootElement.GetProperty("response")[0];
        var firstName = response.GetProperty("first_name").GetString();
        var lastName = response.GetProperty("last_name").GetString();
        var photo = response.TryGetProperty("photo_200", out var ph) ? ph.GetString() : null;

        // Find or create user
        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            user = await _userManager.FindByEmailAsync(email);
        }

        if (user == null)
        {
            // Try by username vk_{id}
            var userName = $"vk_{vkUserId}";
            user = await _userManager.FindByNameAsync(userName);
        }

        if (user == null)
        {
            var newUser = new ApplicationUser
            {
                UserName = !string.IsNullOrWhiteSpace(email) ? email : $"vk_{vkUserId}@vk.local",
                Email = !string.IsNullOrWhiteSpace(email) ? email : null,
                DisplayName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                EmailConfirmed = true
            };
            var createRes = await _userManager.CreateAsync(newUser);
            if (!createRes.Succeeded)
            {
                // fallback: return errors
                return BadRequest(createRes.Errors);
            }
            user = newUser;
        }

        // Optionally update avatar
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
