using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;

namespace PalmMap.Api.Controllers;

/// <summary>
/// API для интеграции с городскими системами.
/// Требует API-ключ в заголовке X-Api-Key.
/// </summary>
[ApiController]
[Route("api/v1/integration")]
[EnableRateLimiting("api")]
public class IntegrationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public IntegrationController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>
    /// Проверка API-ключа
    /// </summary>
    private bool ValidateApiKey()
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var validKey = Environment.GetEnvironmentVariable("INTEGRATION_API_KEY") 
            ?? _configuration.GetValue<string>("Integration:ApiKey");
        
        if (string.IsNullOrWhiteSpace(validKey))
        {
            // В режиме разработки без ключа - разрешаем доступ
            return _configuration.GetValue<bool>("Integration:AllowAnonymous", false);
        }
        
        return apiKey == validKey;
    }

    /// <summary>
    /// Получить все места с агрегированной статистикой отзывов
    /// GET /api/v1/integration/places
    /// </summary>
    [HttpGet("places")]
    [ProducesResponseType(typeof(List<PlaceExportDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetPlaces(
        [FromQuery] string? type = null,
        [FromQuery] double? minRating = null,
        [FromQuery] int? minReviews = null,
        [FromQuery] int limit = 1000,
        [FromQuery] int offset = 0)
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        var query = _db.Places.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.Type == type);

        var places = await query
            .Skip(offset)
            .Take(Math.Min(limit, 1000))
            .Select(p => new PlaceExportDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Type = p.Type,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Address = p.Address,
                ReviewCount = _db.Reviews.Count(r => r.PlaceId == p.Id.ToString()),
                AverageRating = _db.Reviews
                    .Where(r => r.PlaceId == p.Id.ToString())
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        // Фильтрация по рейтингу и количеству отзывов (после агрегации)
        if (minRating.HasValue)
            places = places.Where(p => p.AverageRating >= minRating.Value).ToList();
        
        if (minReviews.HasValue)
            places = places.Where(p => p.ReviewCount >= minReviews.Value).ToList();

        return Ok(new
        {
            total = places.Count,
            offset,
            limit,
            data = places
        });
    }

    /// <summary>
    /// Получить детальную информацию о месте
    /// GET /api/v1/integration/places/{id}
    /// </summary>
    [HttpGet("places/{id}")]
    [ProducesResponseType(typeof(PlaceDetailExportDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPlace(string id)
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        var place = await _db.Places
            .Where(p => p.Id.ToString() == id)
            .Select(p => new PlaceDetailExportDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Type = p.Type,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Address = p.Address,
                CreatedAt = p.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (place == null)
            return NotFound(new { error = "Place not found" });

        // Добавляем статистику отзывов
        var reviews = await _db.Reviews
            .Where(r => r.PlaceId == id)
            .ToListAsync();

        place.ReviewCount = reviews.Count;
        place.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
        place.RatingDistribution = new Dictionary<int, int>
        {
            [1] = reviews.Count(r => r.Rating == 1),
            [2] = reviews.Count(r => r.Rating == 2),
            [3] = reviews.Count(r => r.Rating == 3),
            [4] = reviews.Count(r => r.Rating == 4),
            [5] = reviews.Count(r => r.Rating == 5)
        };

        return Ok(place);
    }

    /// <summary>
    /// Получить отзывы для места
    /// GET /api/v1/integration/places/{id}/reviews
    /// </summary>
    [HttpGet("places/{id}/reviews")]
    [ProducesResponseType(typeof(List<ReviewExportDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetPlaceReviews(
        string id,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery] string sort = "newest")
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        var query = _db.Reviews
            .Where(r => r.PlaceId == id)
            .Include(r => r.Votes);

        var orderedQuery = sort switch
        {
            "oldest" => query.OrderBy(r => r.CreatedAt),
            "rating_high" => query.OrderByDescending(r => r.Rating),
            "rating_low" => query.OrderBy(r => r.Rating),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var reviews = await orderedQuery
            .Skip(offset)
            .Take(Math.Min(limit, 100))
            .Select(r => new ReviewExportDto
            {
                Id = r.Id.ToString(),
                PlaceId = r.PlaceId,
                Rating = r.Rating,
                Comment = r.Comment,
                HasPhoto = r.PhotoPath != null,
                Likes = r.Votes.Count(v => v.IsLike),
                Dislikes = r.Votes.Count(v => !v.IsLike),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        var total = await _db.Reviews.CountAsync(r => r.PlaceId == id);

        return Ok(new
        {
            total,
            offset,
            limit,
            data = reviews
        });
    }

    /// <summary>
    /// Получить общую статистику платформы
    /// GET /api/v1/integration/stats
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PlatformStatsDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetStats()
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        var stats = new PlatformStatsDto
        {
            TotalPlaces = await _db.Places.CountAsync(),
            TotalReviews = await _db.Reviews.CountAsync(),
            TotalUsers = await _db.Users.CountAsync(),
            AverageRating = await _db.Reviews.AverageAsync(r => (double?)r.Rating) ?? 0,
            ReviewsLast7Days = await _db.Reviews
                .CountAsync(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-7)),
            ReviewsLast30Days = await _db.Reviews
                .CountAsync(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-30)),
            TopCategories = await _db.Places
                .GroupBy(p => p.Type)
                .Select(g => new CategoryStatDto
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Count)
                .Take(10)
                .ToListAsync(),
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(stats);
    }

    /// <summary>
    /// Получить места в заданном радиусе от точки
    /// GET /api/v1/integration/places/nearby
    /// </summary>
    [HttpGet("places/nearby")]
    [ProducesResponseType(typeof(List<PlaceExportDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetNearbyPlaces(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double radiusKm = 1.0,
        [FromQuery] string? type = null,
        [FromQuery] int limit = 50)
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        // Простой расчёт расстояния (приближённый, для небольших расстояний)
        // 1 градус широты ≈ 111 км
        // 1 градус долготы ≈ 111 * cos(широта) км
        var latDelta = radiusKm / 111.0;
        var lonDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180));

        var query = _db.Places
            .Where(p => p.Latitude >= lat - latDelta && p.Latitude <= lat + latDelta)
            .Where(p => p.Longitude >= lon - lonDelta && p.Longitude <= lon + lonDelta);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.Type == type);

        var places = await query
            .Take(Math.Min(limit, 100))
            .Select(p => new PlaceExportDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Type = p.Type,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Address = p.Address,
                ReviewCount = _db.Reviews.Count(r => r.PlaceId == p.Id.ToString()),
                AverageRating = _db.Reviews
                    .Where(r => r.PlaceId == p.Id.ToString())
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        // Вычисляем точное расстояние и фильтруем
        places = places
            .Select(p => {
                p.DistanceKm = CalculateDistance(lat, lon, p.Latitude, p.Longitude);
                return p;
            })
            .Where(p => p.DistanceKm <= radiusKm)
            .OrderBy(p => p.DistanceKm)
            .ToList();

        return Ok(new
        {
            center = new { lat, lon },
            radiusKm,
            total = places.Count,
            data = places
        });
    }

    /// <summary>
    /// Экспорт данных в формате GeoJSON
    /// GET /api/v1/integration/export/geojson
    /// </summary>
    [HttpGet("export/geojson")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ExportGeoJson([FromQuery] string? type = null)
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing API key" });

        var query = _db.Places.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.Type == type);

        var places = await query.ToListAsync();

        var features = places.Select(p => new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { p.Longitude, p.Latitude }
            },
            properties = new
            {
                id = p.Id.ToString(),
                name = p.Name,
                placeType = p.Type,
                address = p.Address,
                reviewCount = _db.Reviews.Count(r => r.PlaceId == p.Id.ToString()),
                averageRating = _db.Reviews
                    .Where(r => r.PlaceId == p.Id.ToString())
                    .Select(r => (double?)r.Rating)
                    .Average() ?? 0
            }
        });

        var geoJson = new
        {
            type = "FeatureCollection",
            features
        };

        return Ok(geoJson);
    }

    /// <summary>
    /// Расчёт расстояния между двумя точками (формула Haversine)
    /// </summary>
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Радиус Земли в км
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}

#region DTOs

public class PlaceExportDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public int ReviewCount { get; set; }
    public double AverageRating { get; set; }
    public double? DistanceKm { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlaceDetailExportDto : PlaceExportDto
{
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}

public class ReviewExportDto
{
    public string Id { get; set; } = string.Empty;
    public string PlaceId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool HasPhoto { get; set; }
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlatformStatsDto
{
    public int TotalPlaces { get; set; }
    public int TotalReviews { get; set; }
    public int TotalUsers { get; set; }
    public double AverageRating { get; set; }
    public int ReviewsLast7Days { get; set; }
    public int ReviewsLast30Days { get; set; }
    public List<CategoryStatDto> TopCategories { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class CategoryStatDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

#endregion
