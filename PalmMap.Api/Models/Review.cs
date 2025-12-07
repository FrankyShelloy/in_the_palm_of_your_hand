namespace PalmMap.Api.Models;

public enum ModerationStatus
{
    Pending = 0,    // На модерации
    Approved = 1,   // Одобрен
    Rejected = 2    // Отклонён
}

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    // Связь с объектом на карте
    public string PlaceId { get; set; } = string.Empty;
    public string PlaceName { get; set; } = string.Empty;
    
    // Рейтинг от 1 до 5 (общий рейтинг, если указан напрямую)
    public int Rating { get; set; }
    
    // Оценки по критериям (JSON строка с ключами критериев и значениями 1-5)
    // Формат: {"criterion1": 5, "criterion2": 4, ...}
    // Если заполнено, то Rating вычисляется как среднее из этих значений
    public string? CriteriaRatings { get; set; }
    
    // Флаг: true если рейтинг указан напрямую, false если вычислен из критериев
    public bool IsDirectRating { get; set; } = true;
    
    // Комментарий (необязательный)
    public string? Comment { get; set; }
    
    // Фотография (путь к файлу)
    public string? PhotoPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Статус модерации
    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Pending;
    
    // Причина отклонения (если отклонён)
    public string? RejectionReason { get; set; }
    
    // Дата модерации
    public DateTime? ModeratedAt { get; set; }
    
    // ID модератора
    public string? ModeratorId { get; set; }

    public ICollection<ReviewVote> Votes { get; set; } = new List<ReviewVote>();
}
