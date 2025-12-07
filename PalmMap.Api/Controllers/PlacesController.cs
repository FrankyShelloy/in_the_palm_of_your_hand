using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalmMap.Api.Data;
using PalmMap.Api.Dtos;
using PalmMap.Api.Models;
using PalmMap.Api.Services;

namespace PalmMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlacesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AchievementService _achievementService;

    public PlacesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AchievementService achievementService)
    {
        _context = context;
        _userManager = userManager;
        _achievementService = achievementService;
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

        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _achievementService.CheckAndAwardAsync(user);
            }
        }

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
