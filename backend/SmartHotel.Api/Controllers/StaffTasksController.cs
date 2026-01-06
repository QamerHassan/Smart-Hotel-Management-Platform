using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Api.Hubs;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager,Receptionist")]
public class StaffTasksController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly IHubContext<HotelHub> _hubContext;

    public StaffTasksController(HotelDbContext context, IHubContext<HotelHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks()
    {
        var tasks = await _context.StaffTasks
            .Include(t => t.Room)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Type,
                t.RoomId,
                RoomNumber = t.Room != null ? t.Room.RoomNumber : "N/A",
                t.CreatedAt
            })
            .ToListAsync();
            
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] StaffTask task)
    {
        _context.StaffTasks.Add(task);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.Id, task.Status);

        return Ok(task);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var task = await _context.StaffTasks.FindAsync(id);
        if (task == null) return NotFound();

        task.Status = status;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.Id, task.Status);

        // If task is cleaning and completed, mark room as available
        if (task.Type == "Cleaning" && status == "Completed")
        {
            var room = await _context.Rooms.FindAsync(task.RoomId);
            if (room != null)
            {
                room.Status = "Available";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveRoomUpdate", room.Id, room.Status);
            }
        }

        return Ok(task);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTasksByStatus([FromQuery] string status)
    {
        if (string.IsNullOrEmpty(status)) return BadRequest("Status is required");

        var tasksToDelete = await _context.StaffTasks
            .Where(t => t.Status == status)
            .ToListAsync();

        if (!tasksToDelete.Any()) return NotFound("No tasks found with that status");

        _context.StaffTasks.RemoveRange(tasksToDelete);
        await _context.SaveChangesAsync();

        // Notify clients to remove these tasks
        // We can optimize this by sending a "BatchDelete" event or just individual deletes
        // For now, let's trigger a refresh or send a generic "RefreshTasks" ? 
        // Or send multiple "ReceiveTaskDelete" events?
        // Let's send a specific update indicating batch action.
        await _hubContext.Clients.All.SendAsync("ReceiveBatchDelete", status);

        return NoContent();
    }
}
