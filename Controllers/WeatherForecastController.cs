using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.AuthDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Infrastructure.Hangfire;
using GoWork.Models;
using GoWork.Services.AdminService;
using GoWork.Services.JobService;
using GoWork.Services.NotificationService;
using Hangfire;
<<<<<<< HEAD
using Hangfire.Common;
=======
using Microsoft.AspNetCore.Identity;
>>>>>>> 78c2748c5cdef0dc8372631d9959ff1830f06327
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
        private readonly UserManager<ApplicationUser> _userManager;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IAdminService adminService, ApplicationDbContext context, INotificationService notificationService,UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _notificationService = notificationService;
            _adminService = adminService;
            _userManager = userManager;

        }

        [HttpPost("hangfire")]
        public IActionResult TestHangfire()
        {
            BackgroundJob.Schedule<JobExpirationService>(
                s => s.SayHello(),
                TimeSpan.FromSeconds(30));

            return Ok("Job scheduled");
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
            //await _notificationService.SendToTopicAsync("all", "Test Message", "Hi there", NotificationTypeEnum.General);
            var jobName = "Software Engineer";

            await _notificationService.SendToTopicAsync(
                "",
                "وظيفة جديدة بانتظارك!",
                $"تمت إضافة وظيفة جديدة بعنوان \"\u2068{jobName}\u2069\". لا تفوّت الفرصة، اضغط للاطلاع على التفاصيل والتقديم.",
                NotificationTypeEnum.JobCreated);
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

        [HttpPost("ResetPAssword")]

        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ResetPAssword(ForgetPasswordDTO forgetPasswordDTO)
        {
            var user = await _userManager.FindByEmailAsync(forgetPasswordDTO.Email);

            //if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            //    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid request"); // Prevent account enumeration

            if (user == null)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Email Not Found, Go to register"); // Prevent account enumeration

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, "Mohammed$1");

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Password reset successfully"
            });
        }
    }
}
