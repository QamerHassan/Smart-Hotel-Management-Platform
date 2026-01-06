using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Infrastructure.Data;
using SmartHotel.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHotel.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly HotelDbContext _context;
        public InventoryController(HotelDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory() 
            => await _context.InventoryItems.ToListAsync();

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryItem>> GetInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryItem>> PutInventoryItem(int id, InventoryItem item)
        {
            if (id != item.Id) return BadRequest();
            
            var existingItem = await _context.InventoryItems.FindAsync(id);
            if (existingItem == null) return NotFound();
            
            existingItem.Name = item.Name;
            existingItem.Category = item.Category;
            existingItem.StockLevel = item.StockLevel;
            existingItem.MinimumRequired = item.MinimumRequired;
            existingItem.Unit = item.Unit;
            
            await _context.SaveChangesAsync();
            return Ok(existingItem);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryItem>> PostInventoryItem(InventoryItem item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryItem(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound();
            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
