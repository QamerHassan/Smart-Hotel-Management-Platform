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
public class RoomsController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly IHubContext<HotelHub> _hubContext;

    public RoomsController(HotelDbContext context, IHubContext<HotelHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetRooms()
    {
        return Ok(await _context.Rooms.ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoom(int id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null) return NotFound();
        return Ok(room);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] Room room)
    {
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveRoomUpdate", room.Id, "Created");
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoom(int id, [FromBody] Room updatedRoom)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        room.RoomNumber = updatedRoom.RoomNumber;
        room.RoomType = updatedRoom.RoomType;
        room.BasePrice = updatedRoom.BasePrice;
        room.Capacity = updatedRoom.Capacity;
        room.Amenities = updatedRoom.Amenities;
        // Status might be handled separately or here, but let's allow full update
        room.Status = updatedRoom.Status;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveRoomUpdate", room.Id, "Updated");

        return Ok(room);
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        // Check for dependencies (bookings)
        // Ideally we soft-delete or block if active bookings exist.
        // For this MVP, we'll allow delete (cascade might fail if DB constrained, so try/catch)
        try 
        {
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveRoomUpdate", id, "Deleted");
            return NoContent();
        }
        catch
        {
            return BadRequest("Cannot delete room. It may have active bookings.");
        }
    }

    [Authorize(Roles = "Admin,Manager,Receptionist")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var room = await _context.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        room.Status = status;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveRoomUpdate", room.Id, room.Status);

        return Ok(room);
    }
}
