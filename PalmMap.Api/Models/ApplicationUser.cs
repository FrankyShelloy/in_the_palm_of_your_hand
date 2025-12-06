using Microsoft.AspNetCore.Identity;

namespace PalmMap.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public int Level { get; set; } = 1;
    public int ReviewCount { get; set; }
    public int Points { get; set; } = 0; // Очки за активность

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
