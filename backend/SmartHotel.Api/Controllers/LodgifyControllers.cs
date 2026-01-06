using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Infrastructure.Data;
using SmartHotel.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHotel.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly HotelDbContext _context;
        public MessagesController(HotelDbContext context) => _context = context;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Message>>> GetMessages() => await _context.Messages.ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Message>> PostMessage(Message message)
        {
            message.Timestamp = DateTime.UtcNow;
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMessages), new { id = message.Id }, message);
        }
    }


    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly HotelDbContext _context;
        private readonly SmartHotel.Domain.Interfaces.IPricingService _aiService;

        public ReviewsController(HotelDbContext context, SmartHotel.Domain.Interfaces.IPricingService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviews() => await _context.Reviews.ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Review>> PostReview(Review review)
        {
            review.Date = DateTime.UtcNow;
            
            // Calculate overall rating as average of category ratings
            review.Rating = (review.StaffRating + review.CleanlinessRating + review.ComfortRating + review.ValueRating) / 4.0;
            
            // AI Integration: Analyze Sentiment
            var analysis = await _aiService.AnalyzeSentiment(review.Comment);
            review.Sentiment = analysis.Sentiment;
            review.SentimentScore = analysis.Score;
            
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetReviews), new { id = review.Id }, review);
        }
    }

}
