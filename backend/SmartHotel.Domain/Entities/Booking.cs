namespace SmartHotel.Domain.Entities;

public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Checked-In, Checked-Out, Cancelled
    public decimal FinalPrice { get; set; }
    public int? UserId { get; set; }
}
