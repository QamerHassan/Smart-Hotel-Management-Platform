namespace SmartHotel.Domain.Entities;

public class Room
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public int Capacity { get; set; } = 2;
    public string Status { get; set; } = "Available"; // Available, Occupied, Cleaning, Maintenance
    public string? Amenities { get; set; } // Comma-separated list (e.g. "Wifi,AC,MiniBar")
}
