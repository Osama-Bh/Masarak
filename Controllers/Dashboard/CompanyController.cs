using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GoWork.Controllers.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CompanyController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<int?> GetEmployerIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return null;

            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == userId);
            return employer?.Id;
        }

        private IQueryable<GoWork.Models.Application> BuildCompanyApplicationsBaseQuery(int employerId)
        {
            return _context.TbApplications.Where(a => a.Job.EmployerId == employerId);
        }

        private IQueryable<GoWork.Models.Application> FilterCompanyHistoricalApplications(IQueryable<GoWork.Models.Application> query)
        {
            var today = DateTime.UtcNow.Date;

            return query.Where(a =>
                (
                    a.ApplicationStatusId == (int)ApplicationStatusEnum.Withdrawn &&
                    a.ApplicationDate.Date < today
                )
                ||
                (
                    a.ApplicationStatusId == (int)ApplicationStatusEnum.MissingInterview &&
                    a.Interviews
                        .OrderByDescending(i => i.InterviewDate)
                        .Select(i => (DateTime?)i.InterviewDate.Date)
                        .FirstOrDefault() < today
                )
                ||
                (
                    (a.ApplicationStatusId == (int)ApplicationStatusEnum.Rejected ||
                     a.ApplicationStatusId == (int)ApplicationStatusEnum.Hired) &&
                    (
                        (
                            a.Interviews.Any() &&
                            a.Interviews
                                .OrderByDescending(i => i.InterviewDate)
                                .Select(i => (DateTime?)i.InterviewDate.Date)
                                .FirstOrDefault() < today
                        )
                        ||
                        (
                            !a.Interviews.Any() &&
                            a.ApplicationDate.Date < today
                        )
                    )
                )
            );
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("test-auth")]
        public IActionResult TestAuth()
        {
            return Ok(new
            {
                Message = "Authentication successful",
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Email = User.FindFirstValue(ClaimTypes.Email),
                Roles = User.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList()
            });
        }

        [Authorize(Roles = "Company,Admin")]
        [HttpGet("dashboard-statistics")]
        public async Task<ActionResult<ApiResponse<CompanyDashboardStatisticsDTO>>> GetDashboardStatistics()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var utcNow = DateTime.UtcNow;
            var today = utcNow.Date;
            var tomorrow = today.AddDays(1);
            var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
            var startOfWeek = today.AddDays(-daysSinceMonday);

            var jobs = _context.TbJobs.Where(j => j.EmployerId == employerId.Value);
            var applications = BuildCompanyApplicationsBaseQuery(employerId.Value);
            var historicalApplications = FilterCompanyHistoricalApplications(applications);
            var interviews = _context.TbInterviews.Where(i => i.Application.Job.EmployerId == employerId.Value);

            var stats = new CompanyDashboardStatisticsDTO
            {
                ActiveJobsNow = await jobs.CountAsync(j => j.JobStatusId == (int)JobStatusEnum.Published && j.ExpirationDate >= utcNow),
                NewApplicantsThisWeek = await applications.CountAsync(a => a.ApplicationDate >= startOfWeek),
                InterviewsToday = await interviews.CountAsync(i => i.InterviewDate >= today && i.InterviewDate < tomorrow),
                SuccessfulHires = await historicalApplications.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Hired)
            };

            return Ok(new ApiResponse<CompanyDashboardStatisticsDTO>(200, stats));
        }
    }
}
