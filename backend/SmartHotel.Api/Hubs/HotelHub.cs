using Microsoft.AspNetCore.SignalR;

namespace SmartHotel.Api.Hubs;

public class HotelHub : Hub
{
    public async Task SendRoomUpdate(int roomId, string status)
    {
        await Clients.All.SendAsync("ReceiveRoomUpdate", roomId, status);
    }

    public async Task SendBookingUpdate(int bookingId, string status)
    {
        await Clients.All.SendAsync("ReceiveBookingUpdate", bookingId, status);
    }

    public async Task SendConciergeUpdate(object request)
    {
        await Clients.All.SendAsync("ReceiveConciergeUpdate", request);
    }
}
