using GoWork.Data;
using GoWork.Services.NotificationService;
using Microsoft.AspNetCore.Mvc;

namespace GoWork.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        public WeatherForecastController(ILogger<WeatherForecastController> logger, ApplicationDbContext context, INotificationService notificationService)
        {
            _logger = logger;
            _context = context;
            _notificationService = notificationService;
        }

        // hi there 
        [HttpGet("ApplicationStatuses")]
        public string GetApplicationStatuses()
        {
            var  applicationStatuses = string.Join(", ", _context.TbApplicationStatuses.Select(s => s.Name).ToList());
            return applicationStatuses;
        }

        [HttpGet("InterviewStatuses")]
        public string GetInterviewsStatuses()
        {
            var InterviewStatuses = string.Join(", ", _context.TbInterviewStatuses.Select(s => s.Name).ToList());
            return InterviewStatuses;
        }

        [HttpPost("pushnotification")]
        public async Task<IActionResult> SendNotification()
        {
            await _notificationService.SendTopicNotificationAsync("all","Test Message", "Hi there");
            return Ok();
        }

    }
}
