using System;

namespace SmartHotel.Domain.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public required string SenderName { get; set; }
        public required string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsIncoming { get; set; }
        public string? GuestRoom { get; set; }
    }

    public class InventoryItem
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Category { get; set; }
        public int StockLevel { get; set; }
        public int MinimumRequired { get; set; }
        public required string Unit { get; set; }
    }

    public class Review
    {
        public int Id { get; set; }
        public required string GuestName { get; set; }
        public double Rating { get; set; } // Overall rating (calculated average)
        public required string Comment { get; set; }
        public DateTime Date { get; set; }
        public required string Category { get; set; }
        public string? Sentiment { get; set; }
        public float? SentimentScore { get; set; }
        
        // Detailed Category Ratings
        public double StaffRating { get; set; }
        public double CleanlinessRating { get; set; }
        public double ComfortRating { get; set; }
        public double ValueRating { get; set; }
        
        // Structured Feedback Questions
        public string? WouldRecommend { get; set; } // Yes/No
        public string? FavoriteAspect { get; set; }
        public string? ImprovementSuggestion { get; set; }
        public string? RoomNumber { get; set; }
        public int? UserId { get; set; } // Link to authenticated user
    }

    public class ConciergeRequest
    {
        public int Id { get; set; }
        public required string GuestName { get; set; }
        public required string RoomNumber { get; set; }
        public required string RequestDetails { get; set; }
        public DateTime CreatedAt { get; set; }
        public required string Status { get; set; } // Pending, Confirmed, Fulfilled
    }
}
