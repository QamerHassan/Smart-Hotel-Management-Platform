using System.ComponentModel.DataAnnotations;

namespace SmartHotel.Domain.Entities;

public class PriceHistory
{
    public int Id { get; set; }
    
    public string RoomType { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public DateTime Date { get; set; } // The target date for the price
    
    public string Reason { get; set; } = string.Empty; // "AI Prediction", "Weekend Rule", "Manual Override"
    
    public string Source { get; set; } = "AI"; 
    
    public float AiConfidence { get; set; } = 0f;
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
