using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHotel.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : ControllerBase
    {
        private readonly HotelDbContext _context;

        public AuditLogsController(HotelDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLog>>> GetLogs()
        {
            return await _context.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<AuditLog>> CreateLog(AuditLog log)
        {
            log.Timestamp = DateTime.UtcNow;
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetLogs), new { id = log.Id }, log);
        }
    }
}
