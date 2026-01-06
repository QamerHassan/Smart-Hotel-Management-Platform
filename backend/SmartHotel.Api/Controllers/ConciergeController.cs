using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Api.Hubs;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Data;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConciergeController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly IHubContext<HotelHub> _hubContext;

    public ConciergeController(HotelDbContext context, IHubContext<HotelHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetRequests()
    {
        return Ok(await _context.ConciergeRequests.OrderByDescending(r => r.CreatedAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] ConciergeRequest request)
    {
        request.CreatedAt = DateTime.UtcNow;
        request.Status = "Pending";
        
        _context.ConciergeRequests.Add(request);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveConciergeUpdate", request);

        return Ok(request);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var conciergeRequest = await _context.ConciergeRequests.FindAsync(id);
        if (conciergeRequest == null) return NotFound();

        conciergeRequest.Status = request.Status;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveConciergeUpdate", conciergeRequest);

        return Ok(conciergeRequest);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteRequest(int id)
    {
        var request = await _context.ConciergeRequests.FindAsync(id);
        if (request == null) return NotFound();

        _context.ConciergeRequests.Remove(request);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public record UpdateStatusRequest(string Status);
