using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Dtos;
using PalmMap.Api.Models;

namespace PalmMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlacesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PlacesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlaceDto>>> GetAll()
    {
        var places = await _context.Places
            .Select(p => new PlaceDto(
                p.Id,
                p.Name,
                p.Type,
                p.Latitude,
                p.Longitude,
                p.Address,
                p.CreatedByUserId
            ))
            .ToListAsync();

        return places;
    }

    [HttpPost]
    public async Task<ActionResult<PlaceDto>> Create(CreatePlaceDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        // Optional: Require authentication?
        // if (userId == null) return Unauthorized();

        var place = new Place
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Address = dto.Address,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = place.Id }, new PlaceDto(
            place.Id,
            place.Name,
            place.Type,
            place.Latitude,
            place.Longitude,
            place.Address,
            place.CreatedByUserId
        ));
    }
}
