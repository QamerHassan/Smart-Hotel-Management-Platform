using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartHotel.Domain.Interfaces;
using SmartHotel.Domain.Entities;
using SmartHotel.Infrastructure.Data;
using SmartHotel.Infrastructure.Services;
using SmartHotel.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<HotelDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth services
builder.Services.AddScoped<IAuthService, AuthService>();

// AI & Pricing
builder.Services.AddHttpClient<IPricingService, PricingService>();
builder.Services.AddSingleton<IBookingLockService, InMemoryBookingLockService>();

// SignalR
builder.Services.AddSignalR();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "a_very_long_and_secure_secret_key_at_least_32_chars");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hotelHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "http://localhost:3002", "http://localhost:5173", "http://localhost:5174")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// FRESH DATABASE INITIALIZATION
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<HotelDbContext>();
        
        Console.WriteLine("--> Initializing Fresh Database: SmartHotelVibrant");
        
        // This will create the database and all tables from scratch
        // reliably since we are using a new database name.
        context.Database.EnsureCreated();

        // ---------------------------------------------------------
        // EMERGENCY SCHEMA PATCH (Bypassing broken EF Tooling)
        // ---------------------------------------------------------
        try 
        {
            Console.WriteLine("--> Ensuring Tables Exist...");
            // Manually ensure AuditLogs exists if EnsureCreated missed it
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""AuditLogs"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Action"" TEXT DEFAULT '',
                    ""PerformedBy"" TEXT DEFAULT '',
                    ""Details"" TEXT DEFAULT '',
                    ""Timestamp"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    ""EntityType"" TEXT DEFAULT '',
                    ""EntityId"" TEXT DEFAULT ''
                );");

            Console.WriteLine("--> Applying Schema Patches...");
            // Users
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"FullName\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordSalt\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Username\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Role\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Email\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PasswordHash\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"RefreshToken\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"RefreshTokenExpiry\" TIMESTAMP;");

            // StaffTasks
            Console.WriteLine("--> Patching StaffTasks...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"RoomId\" INTEGER DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"Status\" TEXT DEFAULT 'Pending';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"Type\" TEXT DEFAULT 'Cleaning';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"CreatedAt\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"Title\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"Description\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"AssignedToId\" INTEGER;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"StaffTasks\" ADD COLUMN IF NOT EXISTS \"AssignedTo\" TEXT;");

            // Bookings (Critical for Reservations Page)
            Console.WriteLine("--> Patching Bookings...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"RoomId\" INTEGER DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"Status\" TEXT DEFAULT 'Pending';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"CheckIn\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"CheckOut\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"FinalPrice\" NUMERIC(10,2) DEFAULT 0;");

            // AuditLogs (Critical for Global Operations)
            Console.WriteLine("--> Patching AuditLogs...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"Action\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"PerformedBy\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"Details\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"Timestamp\" TIMESTAMP DEFAULT CURRENT_TIMESTAMP;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"EntityType\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"AuditLogs\" ADD COLUMN IF NOT EXISTS \"EntityId\" TEXT DEFAULT '';");

            // Reviews (Sentiment Analysis & Detailed Ratings)
            Console.WriteLine("--> Patching Reviews...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"Sentiment\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"SentimentScore\" FLOAT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"StaffRating\" FLOAT DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"CleanlinessRating\" FLOAT DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"ComfortRating\" FLOAT DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"ValueRating\" FLOAT DEFAULT 0;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"WouldRecommend\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"FavoriteAspect\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"ImprovementSuggestion\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"RoomNumber\" TEXT;");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Reviews\" ADD COLUMN IF NOT EXISTS \"UserId\" INTEGER;");

            // Bookings (Link to User)
            Console.WriteLine("--> Patching Bookings...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"UserId\" INTEGER;");

            // Rooms
            Console.WriteLine("--> Patching Rooms...");
            context.Database.ExecuteSqlRaw("ALTER TABLE \"Rooms\" ADD COLUMN IF NOT EXISTS \"Amenities\" TEXT DEFAULT '';");
            context.Database.ExecuteSqlRaw("UPDATE \"Rooms\" SET \"Amenities\" = '' WHERE \"Amenities\" IS NULL;");

            Console.WriteLine("--> GLOBAL STABILITY PATCH APPLIED SUCCESSFULLY.");
        }
        catch (Exception ex)
        {
             Console.WriteLine($"--> [WARN] Schema patch failed: {ex.Message}");
        }
        // ---------------------------------------------------------
        
        
        // ONE-TIME: Clear old room data and reseed with standardized types
        if (context.Rooms.Any())
        {
            var oldRooms = context.Rooms.ToList();
            if (oldRooms.Any(r => r.RoomType == "Presidential Suite" || r.RoomType == "Royal Penthouse"))
            {
                Console.WriteLine("---> Clearing old room data for standardization...");
                context.Rooms.RemoveRange(oldRooms);
                context.SaveChanges();
            }
        }

        // Seed Rooms if empty
        if (!context.Rooms.Any())
        {
            Console.WriteLine("---> Seeding 35 Premium Rooms across all categories...");
            var rooms = new List<Room>();


            // Deluxe Rooms (101-110)
            rooms.Add(new Room { RoomNumber = "101", RoomType = "Deluxe", BasePrice = 250, Status = "Available", Amenities = "King Bed, City View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "102", RoomType = "Deluxe", BasePrice = 250, Status = "Available", Amenities = "Queen Bed, Garden View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "103", RoomType = "Deluxe", BasePrice = 275, Status = "Occupied", Amenities = "King Bed, Ocean View, Mini Bar, WiFi, Balcony" });
            rooms.Add(new Room { RoomNumber = "104", RoomType = "Deluxe", BasePrice = 250, Status = "Available", Amenities = "Twin Beds, City View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "105", RoomType = "Deluxe", BasePrice = 275, Status = "Available", Amenities = "King Bed, Harbor View, Mini Bar, WiFi, Balcony" });
            rooms.Add(new Room { RoomNumber = "106", RoomType = "Deluxe", BasePrice = 250, Status = "Maintenance", Amenities = "Queen Bed, City View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "107", RoomType = "Deluxe", BasePrice = 250, Status = "Available", Amenities = "King Bed, Garden View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "108", RoomType = "Deluxe", BasePrice = 275, Status = "Occupied", Amenities = "King Bed, Ocean View, Mini Bar, WiFi, Balcony" });
            rooms.Add(new Room { RoomNumber = "109", RoomType = "Deluxe", BasePrice = 250, Status = "Available", Amenities = "Queen Bed, City View, Mini Bar, WiFi" });
            rooms.Add(new Room { RoomNumber = "110", RoomType = "Deluxe", BasePrice = 275, Status = "Available", Amenities = "King Bed, Skyline View, Mini Bar, WiFi, Balcony" });

            // Suite Rooms (201-210)
            rooms.Add(new Room { RoomNumber = "201", RoomType = "Suite", BasePrice = 450, Status = "Available", Amenities = "King Bed, Living Room, Ocean View, Mini Bar, WiFi, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "202", RoomType = "Suite", BasePrice = 450, Status = "Occupied", Amenities = "King Bed, Living Room, City View, Mini Bar, WiFi, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "203", RoomType = "Suite", BasePrice = 480, Status = "Available", Amenities = "King Bed, Living Room, Harbor View, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "204", RoomType = "Suite", BasePrice = 450, Status = "Available", Amenities = "King Bed, Living Room, Garden View, Mini Bar, WiFi, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "205", RoomType = "Suite", BasePrice = 480, Status = "Occupied", Amenities = "King Bed, Living Room, Ocean View, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "206", RoomType = "Suite", BasePrice = 450, Status = "Available", Amenities = "King Bed, Living Room, City View, Mini Bar, WiFi, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "207", RoomType = "Suite", BasePrice = 480, Status = "Available", Amenities = "King Bed, Living Room, Skyline View, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "208", RoomType = "Suite", BasePrice = 450, Status = "Available", Amenities = "King Bed, Living Room, Garden View, Mini Bar, WiFi, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "209", RoomType = "Suite", BasePrice = 480, Status = "Available", Amenities = "King Bed, Living Room, Harbor View, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "210", RoomType = "Suite", BasePrice = 450, Status = "Available", Amenities = "King Bed, Living Room, City View, Mini Bar, WiFi, Jacuzzi" });

            // Penthouse Rooms (301-305)
            rooms.Add(new Room { RoomNumber = "301", RoomType = "Penthouse", BasePrice = 850, Status = "Available", Amenities = "Master Suite, 2 Bedrooms, Panoramic Ocean View, Full Kitchen, Mini Bar, WiFi, Private Terrace, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "302", RoomType = "Penthouse", BasePrice = 850, Status = "Occupied", Amenities = "Master Suite, 2 Bedrooms, City Skyline View, Full Kitchen, Mini Bar, WiFi, Private Terrace, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "303", RoomType = "Penthouse", BasePrice = 900, Status = "Available", Amenities = "Master Suite, 3 Bedrooms, 360° View, Full Kitchen, Mini Bar, WiFi, Private Terrace, Jacuzzi, Wine Cellar" });
            rooms.Add(new Room { RoomNumber = "304", RoomType = "Penthouse", BasePrice = 850, Status = "Available", Amenities = "Master Suite, 2 Bedrooms, Harbor View, Full Kitchen, Mini Bar, WiFi, Private Terrace, Jacuzzi" });
            rooms.Add(new Room { RoomNumber = "305", RoomType = "Penthouse", BasePrice = 900, Status = "Available", Amenities = "Master Suite, 3 Bedrooms, Panoramic Ocean View, Full Kitchen, Mini Bar, WiFi, Private Terrace, Jacuzzi, Wine Cellar" });

            // Grand Vista Rooms (401-405)
            rooms.Add(new Room { RoomNumber = "401", RoomType = "Grand Vista", BasePrice = 650, Status = "Available", Amenities = "King Bed, Floor-to-Ceiling Windows, Ocean Panorama, Living Area, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "402", RoomType = "Grand Vista", BasePrice = 650, Status = "Occupied", Amenities = "King Bed, Floor-to-Ceiling Windows, City Panorama, Living Area, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "403", RoomType = "Grand Vista", BasePrice = 680, Status = "Available", Amenities = "King Bed, Floor-to-Ceiling Windows, Harbor Panorama, Living Area, Mini Bar, WiFi, Jacuzzi, Private Terrace" });
            rooms.Add(new Room { RoomNumber = "404", RoomType = "Grand Vista", BasePrice = 650, Status = "Available", Amenities = "King Bed, Floor-to-Ceiling Windows, Mountain Panorama, Living Area, Mini Bar, WiFi, Jacuzzi, Balcony" });
            rooms.Add(new Room { RoomNumber = "405", RoomType = "Grand Vista", BasePrice = 680, Status = "Available", Amenities = "King Bed, Floor-to-Ceiling Windows, Sunset Panorama, Living Area, Mini Bar, WiFi, Jacuzzi, Private Terrace" });

            // Vibrant Skybox Rooms (501-505)
            rooms.Add(new Room { RoomNumber = "501", RoomType = "Vibrant Skybox", BasePrice = 1200, Status = "Available", Amenities = "Presidential Suite, 2 Master Bedrooms, Rooftop Access, 360° Skyline View, Full Kitchen, Mini Bar, WiFi, Private Pool, Jacuzzi, Wine Cellar" });
            rooms.Add(new Room { RoomNumber = "502", RoomType = "Vibrant Skybox", BasePrice = 1200, Status = "Available", Amenities = "Presidential Suite, 2 Master Bedrooms, Rooftop Access, Ocean & City View, Full Kitchen, Mini Bar, WiFi, Private Pool, Jacuzzi, Wine Cellar" });
            rooms.Add(new Room { RoomNumber = "503", RoomType = "Vibrant Skybox", BasePrice = 1350, Status = "Occupied", Amenities = "Royal Suite, 3 Master Bedrooms, Rooftop Access, Panoramic View, Full Kitchen, Mini Bar, WiFi, Private Pool, Jacuzzi, Wine Cellar, Cinema Room" });
            rooms.Add(new Room { RoomNumber = "504", RoomType = "Vibrant Skybox", BasePrice = 1200, Status = "Available", Amenities = "Presidential Suite, 2 Master Bedrooms, Rooftop Access, Harbor View, Full Kitchen, Mini Bar, WiFi, Private Pool, Jacuzzi, Wine Cellar" });
            rooms.Add(new Room { RoomNumber = "505", RoomType = "Vibrant Skybox", BasePrice = 1350, Status = "Available", Amenities = "Royal Suite, 3 Master Bedrooms, Rooftop Access, 360° Panoramic View, Full Kitchen, Mini Bar, WiFi, Private Pool, Jacuzzi, Wine Cellar, Cinema Room" });

            context.Rooms.AddRange(rooms);
            context.SaveChanges();
            Console.WriteLine($"---> Seeding Complete: {rooms.Count} Premium Rooms Ready.");

        }

        // Seed Inventory if empty
        if (!context.InventoryItems.Any())
        {
            Console.WriteLine("--> Seeding Premium Inventory...");
            var inventory = new List<InventoryItem>
            {
                new InventoryItem { Name = "Egyptian Cotton Sheets", Category = "Linen", StockLevel = 50, MinimumRequired = 20, Unit = "Sets" },
                new InventoryItem { Name = "Organic Silk Pillowcases", Category = "Linen", StockLevel = 45, MinimumRequired = 15, Unit = "Pairs" },
                new InventoryItem { Name = "Luxury Spa Bathrobes", Category = "Linen", StockLevel = 30, MinimumRequired = 10, Unit = "Pieces" },
                new InventoryItem { Name = "Aura Signature Shampoo", Category = "Amenities", StockLevel = 200, MinimumRequired = 50, Unit = "Bottles" },
                new InventoryItem { Name = "Botanical Body Wash", Category = "Amenities", StockLevel = 180, MinimumRequired = 50, Unit = "Bottles" },
                new InventoryItem { Name = "Belgian Chocolates", Category = "Minibar", StockLevel = 15, MinimumRequired = 20, Unit = "Boxes" },
                new InventoryItem { Name = "Artisan Sparkling Water", Category = "Minibar", StockLevel = 60, MinimumRequired = 24, Unit = "Bottles" },
                new InventoryItem { Name = "Nespresso Vertuo Pods", Category = "Housekeeping", StockLevel = 500, MinimumRequired = 100, Unit = "Pods" },
                new InventoryItem { Name = "Premium Cleaning Solution", Category = "Housekeeping", StockLevel = 12, MinimumRequired = 5, Unit = "Liters" }
            };

            context.InventoryItems.AddRange(inventory);
            context.SaveChanges();
            Console.WriteLine("--> Inventory Seeded Successfully.");
        }
        
        Console.WriteLine("--> SmartHotelVibrant is ready for operation.");
    }

    catch (Exception ex)
    {
        Console.WriteLine($"--> [FATAL] Database initialization failed: {ex.Message}");
        if (ex.InnerException != null)
            Console.WriteLine($"--> Inner Exception: {ex.InnerException.Message}");
    }

    // SEED ADMIN USER
    try 
    {
        var context = services.GetRequiredService<HotelDbContext>();
        var authService = services.GetRequiredService<IAuthService>();
        
        if (!context.Users.Any())
        {
             Console.WriteLine("--> Seeding Default Admin User...");
             var adminUser = new User
             {
                 Username = "admin",
                 Email = "admin@smarthotel.com",
                 PasswordHash = authService.HashPassword("admin123"),
                 Role = "Admin",
                 FullName = "System Administrator"
             };
             
             context.Users.Add(adminUser);
             context.SaveChanges();
             Console.WriteLine("--> Admin User Seeded (admin / admin123)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> [ERROR] User seeding failed: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartHotel API V1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "SmartHotel API is running!");
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<HotelHub>("/hotelHub");

app.Run();
