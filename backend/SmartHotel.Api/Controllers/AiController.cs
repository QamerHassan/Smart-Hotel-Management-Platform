using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Domain.Interfaces;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IPricingService _aiService;

    public AiController(IPricingService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("api/chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Guest";
        var reply = await _aiService.ChatWithGenAI(request.Message, role, request.History);
        return Ok(new { reply = reply, status = "success" });
    }

    [HttpGet("api/insights")]
    public async Task<IActionResult> GetInsights()
    {
        var insights = await _aiService.GetTacticalInsights();
        return Ok(insights);
    }

    [HttpGet("api/ai/verify-key")]
    public async Task<IActionResult> VerifyKey()
    {
        var result = await _aiService.TestGeminiKey();
        return Ok(result);
    }

    public record ChatRequest(string Message, dynamic? UserContext, IEnumerable<ChatHistoryItem>? History);
    public record ChatHistoryItem(string Role, string Text);
}
