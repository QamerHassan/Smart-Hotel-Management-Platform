using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Entities;

namespace SmartHotel.Infrastructure.Data;

public class HotelDbContext : DbContext
{
    public HotelDbContext(DbContextOptions<HotelDbContext> options) : base(options)
    {
    }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<StaffTask> StaffTasks { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ConciergeRequest> ConciergeRequests { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<PriceRule> PriceRules { get; set; }
    public DbSet<PriceHistory> PriceHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entities if needed
        modelBuilder.Entity<Room>(entity =>
        {
            entity.Property(e => e.BasePrice).HasColumnType("numeric(10,2)");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(e => e.FinalPrice).HasColumnType("numeric(10,2)");
            
            // Note: Postgres Exclusion Constraint for GIST is better handled via migration or SQL script
            // since EF Core doesn't have a direct attribute for it.
        });
    }
}
