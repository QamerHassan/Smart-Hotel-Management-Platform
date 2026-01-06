using Moq;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Interfaces;
using SmartHotel.Api.Controllers;
using SmartHotel.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using SmartHotel.Api.Hubs;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace SmartHotel.Tests.Services;

public class BookingServiceTests
{
    private readonly HotelDbContext _context;
    private readonly Mock<IHubContext<HotelHub>> _hubContextMock;
    private readonly Mock<IBookingLockService> _lockServiceMock;
    private readonly BookingsController _controller;

    public BookingServiceTests()
    {
        var options = new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new HotelDbContext(options);

        _hubContextMock = new Mock<IHubContext<HotelHub>>();
        
        // Mock SignalR Clients Proxy
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _lockServiceMock = new Mock<IBookingLockService>();

        _controller = new BookingsController(_context, _hubContextMock.Object, _lockServiceMock.Object);
        
        // Setup User Principal for Controller
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.NameIdentifier, "123")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };
    }

    [Fact]
    public async Task CreateBooking_ReturnsConflict_WhenLockCannotBeAcquired()
    {
        // Arrange
        var booking = new Booking { RoomId = 1, CheckIn = DateTime.Today, CheckOut = DateTime.Today.AddDays(1) };
        _lockServiceMock.Setup(l => l.TryAcquireLockAsync(1)).ReturnsAsync(false);

        // Act
        var result = await _controller.CreateBooking(booking);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("The room is currently being booked by another user. Please try again in a moment.", conflictResult.Value);
    }

    [Fact]
    public async Task CreateBooking_ReturnsBadRequest_WhenDatesOverlap()
    {
        // Arrange
        var existingBooking = new Booking 
        { 
            RoomId = 101, 
            CheckIn = DateTime.Today, 
            CheckOut = DateTime.Today.AddDays(5),
            Status = "Confirmed"
        };
        _context.Bookings.Add(existingBooking);
        await _context.SaveChangesAsync();

        var newBooking = new Booking 
        { 
            RoomId = 101, 
            CheckIn = DateTime.Today.AddDays(2), 
            CheckOut = DateTime.Today.AddDays(4) 
        };

        _lockServiceMock.Setup(l => l.TryAcquireLockAsync(101)).ReturnsAsync(true);

        // Act
        var result = await _controller.CreateBooking(newBooking);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Room is already booked for these dates.", badRequest.Value);
    }

    [Fact]
    public async Task CreateBooking_Succeeds_WhenNoOverlapAndLockAcquired()
    {
        // Arrange
        var newBooking = new Booking 
        { 
            RoomId = 202, 
            CheckIn = DateTime.Today.AddDays(10), 
            CheckOut = DateTime.Today.AddDays(12),
            FinalPrice = 500
        };

        _lockServiceMock.Setup(l => l.TryAcquireLockAsync(202)).ReturnsAsync(true);

        // Act
        var result = await _controller.CreateBooking(newBooking);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var createdBooking = Assert.IsType<Booking>(okResult.Value);
        Assert.Equal(202, createdBooking.RoomId);
        Assert.Equal(123, createdBooking.UserId); // Should match mocked claim
    }
}
