namespace PalmMap.Api.Models;

public class ReviewVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ReviewId { get; set; }
    public Review Review { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public bool IsLike { get; set; } // true = like, false = dislike
}
