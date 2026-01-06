using System;

namespace SmartHotel.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty; // e.g., "PRICE_UPDATE", "USER_BAN"
        public string PerformedBy { get; set; } = string.Empty; // Username or specific ID
        public string Details { get; set; } = string.Empty; // JSON or text description
        public DateTime Timestamp { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Room", "User", "Booking"
        public string EntityId { get; set; } = string.Empty;
    }
}
