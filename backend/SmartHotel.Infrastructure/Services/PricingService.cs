using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Interfaces;
using SmartHotel.Infrastructure.Data;

namespace SmartHotel.Infrastructure.Services;

public class PricingService : IPricingService
{
    private readonly HttpClient _httpClient;
    private readonly string _aiServiceUrl;
    private readonly HotelDbContext _context;

    public PricingService(HttpClient httpClient, IConfiguration configuration, HotelDbContext context)
    {
        _httpClient = httpClient;
        _context = context;
        _aiServiceUrl = configuration["AIService:Url"] ?? "http://localhost:8000";
    }

    public async Task<decimal> GetDynamicPrice(string roomType, DateTime date)
    {
        // 1. Base Price (Ideally fetched from RoomType entity, but hardcoded fallback for now)
        decimal basePrice = 100.0m;
        if (roomType.Contains("Presidential")) basePrice = 1200.0m;
        else if (roomType.Contains("Royal")) basePrice = 1500.0m;
        else if (roomType.Contains("Suite")) basePrice = 450.0m;
        else if (roomType.Contains("View")) basePrice = 350.0m;

        decimal multiplier = 1.0m;
        var activeRules = await _context.PriceRules
            .Where(r => r.IsActive && 
                       (!r.StartDate.HasValue || r.StartDate <= date) && 
                       (!r.EndDate.HasValue || r.EndDate >= date))
            .ToListAsync();

        foreach (var rule in activeRules)
        {
            multiplier *= rule.Multiplier;
        }

        // 2. AI Recommendation
        var aiRequest = new { date = date.ToString("yyyy-MM-dd"), room_type = roomType };
        string reason = "Standard Rate";
        float confidence = 1.0f;
        string source = "Rules";

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_aiServiceUrl}/recommend-pricing", aiRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PricingResponse>();
                if (result != null)
                {
                    // Blending Strategy: Average of Rule-Based and AI-Based? 
                    // Or let AI fully take over if confidence is high?
                    // For safety, we'll let AI adjust the multiplier further.
                    
                    // Example: AI suggests $150, Base is $100 -> AI Multiplier is 1.5
                    // We apply AI multiplier on top of rules? Or replace?
                    // Let's treat AI as an advisor that creates a "Dynamic Demand" multiplier.
                    
                    decimal aiPrice = result.RecommendedPrice;
                    decimal aiMultiplier = aiPrice / (basePrice > 0 ? basePrice : 100);
                    
                    // Simple logic: Use AI price directly if no conflicting manual rules, or blend.
                    // Implementation: We'll use the AI price as the base for 'dynamic' factor.
                    // Ignoring the simplistic multiplier logic above for AI specifics.
                    
                    multiplier = aiMultiplier; // bold move: trust AI
                    reason = result.Reason;
                    confidence = result.Confidence;
                    source = "AI";
                }
            }
        }
        catch
        {
            // AI Service down, relying solely on base * rules
             source = "Rules (AI Down)";
        }

        decimal finalPrice = Math.Round(basePrice * multiplier, 2);

        // 3. Log History
        _context.PriceHistory.Add(new PriceHistory
        {
             RoomType = roomType,
             Date = date,
             Price = finalPrice,
             Reason = reason,
             Source = source,
             AiConfidence = confidence
        });
        await _context.SaveChangesAsync();

        return finalPrice;
    }

    public async Task<(string Sentiment, float Score)> AnalyzeSentiment(string text)
    {
        var request = new { text = text };
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_aiServiceUrl}/analyze-sentiment", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SentimentResponse>();
                return (result?.Sentiment ?? "Neutral", result?.Score ?? 0f);
            }
        }
        catch
        {
            // Fallback
        }
        return ("Neutral", 0f);
    }

    public async Task<string> ChatWithGenAI(string message, string role, object? history = null)
    {
        var request = new { message = message, history = history, user_context = new { role = role } };
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_aiServiceUrl}/chat", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
                return result?.Reply ?? "I'm having trouble processing that right now.";
            }
            return "AI Service Unavailable.";
        }
        catch (Exception ex)
        {
            return $"AI Bridge Error: {ex.Message}";
        }
    }

    public async Task<IEnumerable<dynamic>> GetTacticalInsights()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_aiServiceUrl}/insights");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<dynamic>>() ?? new List<dynamic>();
            }
        }
        catch { }
        return new List<dynamic>();
    }

    public async Task<dynamic> TestGeminiKey()
    {
        // ... kept as is or simplified
         try
        {
            var response = await _httpClient.GetAsync($"{_aiServiceUrl}/test-ai");
            return await response.Content.ReadFromJsonAsync<dynamic>() ?? new { status = "error", message = "No response" };
        }
        catch (Exception ex)
        {
            return new { status = "error", message = ex.Message };
        }
    }

    private record PricingResponse(decimal RecommendedPrice, string Reason, float Confidence);
    private record SentimentResponse(string Sentiment, float Score);
    private record ChatResponse(string Reply, string Status);
}
