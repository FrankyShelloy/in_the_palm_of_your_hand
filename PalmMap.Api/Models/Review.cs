namespace PalmMap.Api.Models;

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    // Связь с объектом на карте
    public string PlaceId { get; set; } = string.Empty;
    public string PlaceName { get; set; } = string.Empty;
    
    // Рейтинг от 1 до 5
    public int Rating { get; set; }
    
    // Комментарий (необязательный)
    public string? Comment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
