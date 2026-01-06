namespace SmartHotel.Domain.Entities;

public class StaffTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed
    public string Type { get; set; } = "Cleaning"; // Cleaning, Maintenance
    public int? AssignedToId { get; set; }
    public string? AssignedTo { get; set; }
    public User? AssignedUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
