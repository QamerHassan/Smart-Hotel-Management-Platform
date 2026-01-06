using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartHotel.Infrastructure.Data;

namespace SmartHotel.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly HotelDbContext _context;

    public InvoicesController(HotelDbContext context)
    {
        _context = context;
        // QuestPDF license configuration (required for community use)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [HttpGet("{bookingId}/download")]
    public async Task<IActionResult> DownloadInvoice(int bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return NotFound("Booking not found");
        if (booking.Room == null) return BadRequest("Booking data incomplete (missing room)");

        // Basic authorization check: In a real app, verify User ID matches booking
        // var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // if (booking.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ASTORIA LUXURY HOTEL").SemiBold().FontSize(20).FontColor(Colors.Amber.Medium);
                            col.Item().Text("123 Ocean Drive, Maldives");
                            col.Item().Text("reservations@astoria.com");
                        });

                        row.ConstantItem(100).Column(col =>
                        {
                            col.Item().AlignRight().Text("INVOICE").SemiBold().FontSize(24).FontColor(Colors.Grey.Lighten2);
                            col.Item().AlignRight().Text($"#{booking.Id:D6}");
                            col.Item().AlignRight().Text(DateTime.Now.ToString("d"));
                        });
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Bill To:").SemiBold();
                                c.Item().Text("Guest Name"); // Placeholder if User link missing
                                c.Item().Text("Guest Email"); // Placeholder
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Booking Details:").SemiBold();
                                c.Item().Text($"Check-In: {booking.CheckIn:d}");
                                c.Item().Text($"Check-Out: {booking.CheckOut:d}");
                                c.Item().Text($"Room: {booking.Room.RoomNumber} ({booking.Room.RoomType})");
                            });
                        });

                        col.Item().PaddingVertical(1, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Description").SemiBold();
                                header.Cell().AlignRight().Text("Nights").SemiBold();
                                header.Cell().AlignRight().Text("Rate").SemiBold();
                                header.Cell().AlignRight().Text("Total").SemiBold();
                            });

                            var nights = (booking.CheckOut - booking.CheckIn).Days;
                            nights = Math.Max(1, nights);

                            table.Cell().Text($"Accommodation - {booking.Room.RoomType}");
                            table.Cell().AlignRight().Text(nights.ToString());
                            table.Cell().AlignRight().Text($"${booking.Room.BasePrice:F2}");
                            table.Cell().AlignRight().Text($"${booking.FinalPrice:F2}");

                            table.Cell().ColumnSpan(4).PaddingTop(10).AlignRight().Text($"Subtotal: ${booking.FinalPrice:F2}");
                            table.Cell().ColumnSpan(4).AlignRight().Text("Tax (10%): $" + (booking.FinalPrice * 0.1m).ToString("F2"));
                            table.Cell().ColumnSpan(4).PaddingTop(5).AlignRight().Text($"TOTAL DUE: ${booking.FinalPrice * 1.1m:F2}").Bold().FontSize(14);
                        });
                        
                        // Policy Note
                         col.Item().PaddingTop(2, Unit.Centimetre).Text(text => 
                        {
                            text.Span("Includes Non-Refundable Booking Deposit of $40.00").FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Thank you for choosing Astoria Luxury Hotel.");
                        x.Span(" Page ");
                        x.CurrentPageNumber();
                    });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"Invoice-{bookingId}.pdf");
    }
}
