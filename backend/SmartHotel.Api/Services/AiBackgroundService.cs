using SmartHotel.Domain.Interfaces;

namespace SmartHotel.Api.Services;

public class AiBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiBackgroundService> _logger;

    public AiBackgroundService(IServiceProvider serviceProvider, ILogger<AiBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("AI Background Service is running analysis...");

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var pricingService = scope.ServiceProvider.GetRequiredService<IPricingService>();
                    
                    // Mock a nightly analysis call - checking demand for next Friday
                    var nextFriday = DateTime.Today.AddDays((int)DayOfWeek.Friday - (int)DateTime.Today.DayOfWeek + 7);
                    var demand = await pricingService.GetDynamicPrice("Suite", nextFriday);
                    
                    _logger.LogInformation($"AI Analysis Complete: Projected high demand for {nextFriday:yyyy-MM-dd}. Suggested Suite Price: {demand}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing AI background task.");
            }

            // In real app: run every 24h. For demo: run every 2 minutes.
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }

        _logger.LogInformation("AI Background Service is stopping.");
    }
}
