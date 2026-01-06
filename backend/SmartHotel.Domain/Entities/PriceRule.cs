using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Domain.Entities;

public class PriceRule
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public decimal Multiplier { get; set; } // e.g., 1.2 for +20%
    
    public bool IsActive { get; set; }
    
    public string Category { get; set; } = "Dynamic"; // Seasonal, Dynamic, Strategic, Event
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
