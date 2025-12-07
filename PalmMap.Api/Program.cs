using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PalmMap.Api.Data;
using PalmMap.Api.Models;
using PalmMap.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=app.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

var frontendUrl = builder.Configuration.GetValue<string>("FrontendUrl") ?? "http://localhost";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            var uri = new Uri(origin);
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1") return true;
            var hostParts = uri.Host.Split('.');
            if (hostParts.Length == 4)
            {
                var firstOctet = int.Parse(hostParts[0]);
                var secondOctet = int.Parse(hostParts[1]);
                if ((firstOctet == 192 && secondOctet == 168) ||
                    (firstOctet == 10) ||
                    (firstOctet == 172 && secondOctet >= 16 && secondOctet <= 31))
                {
                    return true;
                }
            }
            return origin.StartsWith(frontendUrl);
        })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? jwtSection["Key"] 
    ?? throw new InvalidOperationException("JWT_SECRET_KEY or Jwt:Key must be configured");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT key must be at least 32 characters long");
}

if (!builder.Environment.IsDevelopment() && jwtKey.Contains("dev_secret_key"))
{
    throw new InvalidOperationException("SECURITY ERROR: Cannot use development JWT key in production. Set JWT_SECRET_KEY environment variable with a secure random key.");
}

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddSingleton<IEmailSenderDev, EmailSender>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/confirm-email", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "confirm-email.html"));
});

app.MapGet("/reset-password", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "reset-password.html"));
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        
        var setAdminIndex = Array.IndexOf(args, "--set-admin");
        if (setAdminIndex >= 0 && setAdminIndex + 1 < args.Length)
        {
            var email = args[setAdminIndex + 1];
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                user.IsAdmin = true;
                db.SaveChanges();
                Console.WriteLine($"✅ User {email} is now admin.");
            }
            else
            {
                Console.WriteLine($"❌ User {email} not found.");
            }
            return; // Exit after setting admin
        }

        var achievement1 = await db.Achievements.FindAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        if (achievement1 != null && achievement1.Code != "first-steps")
        {
            achievement1.Code = "first-steps";
            achievement1.Title = "Первые шаги";
            achievement1.Description = "Начало пути картографа здоровья";
            achievement1.Icon = "👣";
            achievement1.ProgressType = AchievementProgressType.FirstPlaceAdded;
            achievement1.TargetValue = 1;
            achievement1.RequiredReviews = 0;
        }

        var achievement2 = await db.Achievements.FindAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        if (achievement2 != null && achievement2.Code != "attentive-citizen")
        {
            achievement2.Code = "attentive-citizen";
            achievement2.Title = "Внимательный горожанин";
            achievement2.Description = "Проявил внимание к городской среде";
            achievement2.Icon = "👁️";
            achievement2.ProgressType = AchievementProgressType.ReviewsCount;
            achievement2.TargetValue = 10;
            achievement2.RequiredReviews = 10;
        }

        var achievement3 = await db.Achievements.FindAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        if (achievement3 != null && achievement3.Code != "health-photographer")
        {
            achievement3.Code = "health-photographer";
            achievement3.Title = "Фотограф здоровья";
            achievement3.Description = "Визуально документируешь городскую среду";
            achievement3.Icon = "📸";
            achievement3.ProgressType = AchievementProgressType.PhotosCount;
            achievement3.TargetValue = 15;
            achievement3.RequiredReviews = 0;
        }

        var achievement4 = await db.Achievements.FindAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        if (achievement4 == null)
        {
            achievement4 = new Achievement
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Code = "objective-critic",
                Title = "Объективный критик",
                Description = "Помогаешь другим сделать осознанный выбор",
                Icon = "✍️",
                ProgressType = AchievementProgressType.DetailedReviewsCount,
                TargetValue = 5,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement4);
        }

        var achievement5 = await db.Achievements.FindAsync(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        if (achievement5 == null)
        {
            achievement5 = new Achievement
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Code = "balanced-opinions",
                Title = "Баланс мнений",
                Description = "Сбалансированный взгляд на городскую инфраструктуру",
                Icon = "⚖️",
                ProgressType = AchievementProgressType.BalancedReviews,
                TargetValue = 2,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement5);
        }

        var achievement6 = await db.Achievements.FindAsync(Guid.Parse("66666666-6666-6666-6666-666666666666"));
        if (achievement6 == null)
        {
            achievement6 = new Achievement
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Code = "infrastructure-detective",
                Title = "Детектив инфраструктуры",
                Description = "Помогаешь расширять картографию города",
                Icon = "🔍",
                ProgressType = AchievementProgressType.NewPlacesAdded,
                TargetValue = 3,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement6);
        }

        var achievement7 = await db.Achievements.FindAsync(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        if (achievement7 == null)
        {
            achievement7 = new Achievement
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Code = "health-expert",
                Title = "Эксперт здоровья",
                Description = "Стал настоящим гидом по здоровому образу жизни в городе",
                Icon = "🏆",
                ProgressType = AchievementProgressType.HighRatedHealthyPlaces,
                TargetValue = 10,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement7);
        }

        var achievement8 = await db.Achievements.FindAsync(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        if (achievement8 == null)
        {
            achievement8 = new Achievement
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Code = "platform-legend",
                Title = "Легенда платформы",
                Description = "Признанное сообществом лицо проекта",
                Icon = "👑",
                ProgressType = AchievementProgressType.TopThreeRating,
                TargetValue = 1,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement8);
        }

        var achievement9 = await db.Achievements.FindAsync(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        if (achievement9 == null)
        {
            achievement9 = new Achievement
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Code = "team-player",
                Title = "Командный игрок",
                Description = "Твои находки полезны сообществу",
                Icon = "🤝",
                ProgressType = AchievementProgressType.PlacesReviewedByOthers,
                TargetValue = 5,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement9);
        }

        var achievement10 = await db.Achievements.FindAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        if (achievement10 == null)
        {
            achievement10 = new Achievement
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Code = "emotional-analyst",
                Title = "Эмоциональный аналитик",
                Description = "Умеешь различать нюансы качества",
                Icon = "💭",
                ProgressType = AchievementProgressType.AllRatingsUsed,
                TargetValue = 5,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement10);
        }

        var achievement11 = await db.Achievements.FindAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        if (achievement11 == null)
        {
            achievement11 = new Achievement
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Code = "fast-fingers",
                Title = "Быстрые пальцы",
                Description = "Активный день исследователя",
                Icon = "⚡",
                ProgressType = AchievementProgressType.PlacesInOneDay,
                TargetValue = 3,
                RequiredReviews = 0
            };
            db.Achievements.Add(achievement11);
        }

        var testAchievementId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var testAchievement = await db.Achievements.FindAsync(testAchievementId);
        if (testAchievement != null)
        {
            var testAchievementUserRecords = await db.UserAchievements
                .Where(ua => ua.AchievementId == testAchievementId)
                .ToListAsync();
            if (testAchievementUserRecords.Any())
            {
                db.UserAchievements.RemoveRange(testAchievementUserRecords);
            }
            
            db.Achievements.Remove(testAchievement);
            await db.SaveChangesAsync();
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string testEmail = "test@healthmap.local";
        var testUser = await userManager.FindByEmailAsync(testEmail);
        if (testUser != null)
        {
            var testUserReviews = await db.Reviews
                .Where(r => r.UserId == testUser.Id)
                .ToListAsync();
            db.Reviews.RemoveRange(testUserReviews);
            
            var testUserAchievements = await db.UserAchievements
                .Where(ua => ua.UserId == testUser.Id)
                .ToListAsync();
            db.UserAchievements.RemoveRange(testUserAchievements);
            
            await db.SaveChangesAsync();
            
            await userManager.DeleteAsync(testUser);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed on startup.");
    }
}

app.Run();
