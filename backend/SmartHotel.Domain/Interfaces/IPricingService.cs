namespace SmartHotel.Domain.Interfaces;

public interface IPricingService
{
    Task<decimal> GetDynamicPrice(string roomType, DateTime date);
    Task<(string Sentiment, float Score)> AnalyzeSentiment(string text);
    Task<string> ChatWithGenAI(string message, string role, object? history = null);
    Task<IEnumerable<dynamic>> GetTacticalInsights();
    Task<dynamic> TestGeminiKey();
}
