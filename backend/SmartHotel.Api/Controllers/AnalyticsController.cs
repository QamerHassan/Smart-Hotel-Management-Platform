using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Infrastructure.Data;

namespace SmartHotel.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly HotelDbContext _context;

    public AnalyticsController(HotelDbContext context)
    {
        _context = context;
    }

    [HttpGet("leaderboard/staff")]
    public async Task<IActionResult> GetStaffLeaderboard()
    {
        // Rank staff by number of completed tasks
        // Note: AssignedToId must be non-null. StaffTask entity was updated to include this.
        var leaderboard = await _context.StaffTasks
            .Where(t => t.Status == "Completed" && t.AssignedToId != null)
            .GroupBy(t => t.AssignedToId)
            .Select(g => new
            {
                StaffId = g.Key ?? 0,
                CompletedTasks = g.Count()
            })
            .OrderByDescending(x => x.CompletedTasks)
            .Take(5)
            .ToListAsync();

        var staffIds = leaderboard.Select(l => l.StaffId).ToList();
        
        // Fetch user details. User entity uses 'Email', not 'FullName'
        var users = await _context.Users
            .Where(u => staffIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email); // Using Email as name fallback

        var result = leaderboard.Select(l => new
        {
            Name = users.ContainsKey(l.StaffId) ? users[l.StaffId] : $"Staff #{l.StaffId}",
            Score = l.CompletedTasks,
            Avatar = "https://i.pravatar.cc/150?u=" + l.StaffId
        });

        return Ok(result);
    }

    [HttpGet("leaderboard/rooms")]
    public async Task<IActionResult> GetRoomLeaderboard()
    {
        // Rank rooms by total revenue from Confirmed bookings
        var leaderboard = await _context.Bookings
            .Where(b => b.Status != "Cancelled")
            .GroupBy(b => b.RoomId)
            .Select(g => new
            {
                RoomId = g.Key,
                Revenue = g.Sum(b => b.FinalPrice),
                BookingsCount = g.Count()
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync();

        var roomIds = leaderboard.Select(l => l.RoomId).ToList();
        var rooms = await _context.Rooms
            .Where(r => roomIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r);

        var result = leaderboard.Select(l => new
        {
            Name = rooms.ContainsKey(l.RoomId) ? $"{rooms[l.RoomId].RoomNumber} - {rooms[l.RoomId].RoomType}" : $"Room #{l.RoomId}",
            Score = l.Revenue,
            SubScore = $"{l.BookingsCount} bookings",
            Avatar = "https://images.unsplash.com/photo-1631049307264-da0ec9d70304?auto=format&fit=crop&q=80&w=100&h=100"
        });

        return Ok(result);
    }

    [HttpGet("revenue/trend")]
    public async Task<IActionResult> GetRevenueTrend()
    {
        // Simple aggregate by month for the current year
        var trend = await _context.Bookings
            .Where(b => b.Status != "Cancelled" && b.CheckIn.Year == DateTime.UtcNow.Year)
            .GroupBy(b => b.CheckIn.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Sum(b => b.FinalPrice)
            })
            .OrderBy(x => x.Month)
            .ToListAsync();

        var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var result = trend.Select(t => new { 
            Name = monthNames[t.Month - 1], 
            Revenue = t.Revenue 
        });

        return Ok(result);
    }

    [HttpGet("occupancy/distribution")]
    public async Task<IActionResult> GetOccupancyStats()
    {
        var stats = await _context.Rooms
            .GroupBy(r => r.Status)
            .Select(g => new
            {
                Name = g.Key,
                Value = g.Count()
            })
            .ToListAsync();

        return Ok(stats);
    }
}
