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

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration, IEmailSenderDev emailSender)
    {
        _userManager = userManager;
        _configuration = configuration;
        _emailSender = emailSender;
        _frontendUrl = configuration.GetValue<string>("FrontendUrl") ?? "http://localhost:5014";
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
            user.ReviewCount
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
