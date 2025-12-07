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
    
    public string PlaceId { get; set; } = string.Empty;
    public string PlaceName { get; set; } = string.Empty;
    
    public int Rating { get; set; }
    
    public string? CriteriaRatings { get; set; }
    
    public bool IsDirectRating { get; set; } = true;
    
    public string? Comment { get; set; }
    
    public string? PhotoPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Pending;
    
    public string? RejectionReason { get; set; }
    
    public DateTime? ModeratedAt { get; set; }
    
    public string? ModeratorId { get; set; }

    public ICollection<ReviewVote> Votes { get; set; } = new List<ReviewVote>();
}
