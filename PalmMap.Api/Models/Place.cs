using System.ComponentModel.DataAnnotations;

namespace PalmMap.Api.Models;

public class Place
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string? Address { get; set; }

    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
