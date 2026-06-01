using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Services.AdminService;
using GoWork.Services.NotificationService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private readonly IAdminService _adminService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IAdminService adminService, ApplicationDbContext context, INotificationService notificationService)
        {
            _logger = logger;
            _context = context;
            _notificationService = notificationService;
            _adminService = adminService;
        }

        // hi there 
        [HttpGet("ApplicationStatuses")]
        public string GetApplicationStatuses()
        {
            var applicationStatuses = string.Join(", ", _context.TbApplicationStatuses.Select(s => s.Name).ToList());
            return applicationStatuses;
        }

        [HttpGet("ApplicationStatusesDetails")]
        public IActionResult GetApplicationStatusesDetails()
        {
            var applicationStatuses = _context.TbApplicationStatuses.Select(s => new
            {
                s.Name,
                s.IsActive
            }).ToList();
            return Ok(applicationStatuses);
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
            await _notificationService.SendTopicNotificationAsync("all", "Test Message", "Hi there");
            return Ok();
        }

        [HttpGet("AvailableCountires")]
        public IActionResult GetCountries()
        {
            var countries = _context.TbCountries.Where(s=>s.IsActive).ToList();

            return Ok(countries);
        }

        [HttpGet("IsEmailExist")]
        public IActionResult IsEmailExists(string Email)
        {
            var IsEmailExists = _context.Users.Where(s => s.Email == Email);

            return Ok(IsEmailExists);
        }

        [HttpGet("TimeZone")]
        public IActionResult TimeZone()
        {
            var TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Aden");

            return Ok(TimeZone);
        }

        [HttpPatch("sub-admins/{id}/status")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateSubAdminStatus(int id, UpdateSubAdminStatusDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _adminService.UpdateSubAdminStatusAsync(id, dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }
    }
}
