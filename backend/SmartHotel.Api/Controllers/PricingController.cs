using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Interfaces;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;
    private readonly HotelDbContext _context;

    public PricingController(IPricingService pricingService, HotelDbContext context)
    {
        _pricingService = pricingService;
        _context = context;
    }

    [HttpGet("dynamic")]
    public async Task<IActionResult> GetDynamicPrice([FromQuery] string roomType, [FromQuery] DateTime date)
    {
        var price = await _pricingService.GetDynamicPrice(roomType, date);
        return Ok(new { Price = price });
    }

    [HttpGet("rules")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetPriceRules()
    {
        var rules = await _context.PriceRules.OrderByDescending(r => r.IsActive).ToListAsync();
        return Ok(rules);
    }

    [HttpPost("rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePriceRule([FromBody] PriceRule rule)
    {
        _context.PriceRules.Add(rule);
        
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "PRICING_RULE_CREATE",
            PerformedBy = User.Identity?.Name ?? "Admin",
            Details = $"Created Price Rule: {rule.Name} ({rule.Multiplier}x)",
            Timestamp = DateTime.UtcNow,
            EntityType = "PriceRule",
            EntityId = "New"
        });

        await _context.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpPut("rules/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePriceRule(int id, [FromBody] PriceRule ruleUpdate)
    {
        var rule = await _context.PriceRules.FindAsync(id);
        if (rule == null) return NotFound();

        rule.Name = ruleUpdate.Name;
        rule.Multiplier = ruleUpdate.Multiplier;
        rule.IsActive = ruleUpdate.IsActive;
        rule.StartDate = ruleUpdate.StartDate;
        rule.EndDate = ruleUpdate.EndDate;
        rule.Category = ruleUpdate.Category;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "PRICING_RULE_UPDATE",
            PerformedBy = User.Identity?.Name ?? "Admin",
            Details = $"Price Rule {id} modified.",
            Timestamp = DateTime.UtcNow,
            EntityType = "PriceRule",
            EntityId = id.ToString()
        });

        await _context.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpDelete("rules/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePriceRule(int id)
    {
        var rule = await _context.PriceRules.FindAsync(id);
        if (rule == null) return NotFound();

        _context.PriceRules.Remove(rule);
        
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "PRICING_RULE_DELETE",
            PerformedBy = User.Identity?.Name ?? "Admin",
            Details = $"Deleted Price Rule: {rule.Name}",
            Timestamp = DateTime.UtcNow,
            EntityType = "PriceRule",
            EntityId = id.ToString()
        });

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("history")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetPriceHistory([FromQuery] string roomType)
    {
        var history = await _context.PriceHistory
            .Where(h => string.IsNullOrEmpty(roomType) || h.RoomType == roomType)
            .OrderByDescending(h => h.Date)
            .Take(50)
            .ToListAsync();
            
        return Ok(history);
    }
}
