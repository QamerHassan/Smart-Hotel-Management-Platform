using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Infrastructure.Data;
using SmartHotel.Domain.Entities;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamificationController : ControllerBase
{
    private readonly HotelDbContext _context;

    public GamificationController(HotelDbContext context)
    {
        _context = context;
    }

    [HttpGet("top-rooms")]
    public async Task<IActionResult> GetTopRooms()
    {
        // Aggregate bookings to find most popular rooms
        var topRooms = await _context.Bookings
            .Include(b => b.Room)
            .Where(b => b.Room != null)
            .GroupBy(b => b.Room!.RoomType)
            .Select(g => new
            {
                RoomType = g.Key,
                BookingCount = g.Count(),
                TotalRevenue = g.Sum(b => b.FinalPrice)
            })
            .OrderByDescending(r => r.BookingCount)
            .Take(5)
            .ToListAsync();

        return Ok(topRooms);
    }

    [HttpGet("top-staff")]
    public async Task<IActionResult> GetTopStaff()
    {
        // Simple leaderboard: count of 'Completed' tasks
        // Assuming we have User/Staff info linkable. 
        // For MVP, we might join on AssignedToId if we had a navigation prop, 
        // but StaffTask only has int? AssignedToId. 
        
        // We will group by AssignedToId and fetch User names separately or use join
        // Using manual join for simplicity if navigation property is missing
        
        var staffStats = await _context.StaffTasks
            .Where(t => t.Status == "Completed" && t.AssignedToId != null)
            .GroupBy(t => t.AssignedToId)
            .Select(g => new 
            {
                StaffId = g.Key,
                TasksCompleted = g.Count() 
            })
            .OrderByDescending(s => s.TasksCompleted)
            .Take(5)
            .ToListAsync();

        // Hydrate names
        var staffIds = staffStats.Select(s => s.StaffId).ToList();
        var users = await _context.Users
            .Where(u => staffIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);

        var leaderboard = staffStats.Select(s => new
        {
            Name = users.ContainsKey(s.StaffId!.Value) ? users[s.StaffId!.Value] : "Unknown",
            s.TasksCompleted,
            Rank = 0 // to be filled client side or by index
        }).ToList();

        return Ok(leaderboard);
    }
}
