using GoWork.Data;
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
        public WeatherForecastController(ILogger<WeatherForecastController> logger,ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
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

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
