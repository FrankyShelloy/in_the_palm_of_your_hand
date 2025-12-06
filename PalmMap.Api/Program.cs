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

// Rate limiting для защиты от брутфорса
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Лимит для авторизации: 5 попыток в минуту
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    
    // Общий лимит API: 100 запросов в минуту
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

// CORS политика
var frontendUrl = builder.Configuration.GetValue<string>("FrontendUrl") ?? "http://localhost";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl)
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

// Security: Warn if using default development key in production
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
// HttpClient used to talk to external providers (VK)
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

// CORS middleware
app.UseCors("AllowFrontend");

// Rate limiting middleware
app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();

// Route HTML files to their physical locations
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

// Ensure database is migrated at startup (apply pending EF migrations)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        
        // Check for --set-admin argument
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
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed on startup.");
        // swallow to allow app to start and return errors via endpoints
    }
}

app.Run();
