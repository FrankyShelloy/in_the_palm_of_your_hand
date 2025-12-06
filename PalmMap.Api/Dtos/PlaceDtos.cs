using System.ComponentModel.DataAnnotations;

namespace PalmMap.Api.Dtos;

public record PlaceDto(
    Guid Id,
    string Name,
    string Type,
    double Latitude,
    double Longitude,
    string? Address,
    string? CreatedByUserId
);

public record CreatePlaceDto(
    [Required] string Name,
    [Required] string Type,
    [Required] double Latitude,
    [Required] double Longitude,
    string? Address
);
