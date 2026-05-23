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
        private static readonly string[] WeeklyArabicLabels =
        {
            "السبت",
            "الأحد",
            "الاثنين",
            "الثلاثاء",
            "الأربعاء",
            "الخميس",
            "الجمعة"
        };

        public CompanyController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static DateTime GetStartOfWeek(DateTime utcNow)
        {
            var daysSinceSaturday = ((int)utcNow.DayOfWeek + 1) % 7;
            return utcNow.Date.AddDays(-daysSinceSaturday);
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

        [Authorize(Roles = "Company,Admin")]
        [HttpGet("dashboard-charts/job-distribution")]
        public async Task<ActionResult<ApiResponse<ChartPieResponseDTO>>> GetJobDistributionChart()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var utcNow = DateTime.UtcNow;
            var jobs = _context.TbJobs.Where(j => j.EmployerId == employerId.Value);

            var items = new List<ChartPieItemDTO>
            {
                new()
                {
                    Key = "active",
                    Label = "نشطة",
                    Value = await jobs.CountAsync(j => j.JobStatusId == (int)JobStatusEnum.Published && j.ExpirationDate >= utcNow),
                    Fill = "var(--chart-1)"
                },
                new()
                {
                    Key = "expired",
                    Label = "منتهية",
                    Value = await jobs.CountAsync(j => j.JobStatusId == (int)JobStatusEnum.Expired || j.ExpirationDate < utcNow),
                    Fill = "var(--chart-2)"
                },
                new()
                {
                    Key = "closed",
                    Label = "مغلقة",
                    Value = await jobs.CountAsync(j => j.JobStatusId == (int)JobStatusEnum.Closed),
                    Fill = "var(--chart-3)"
                },
                new()
                {
                    Key = "filled",
                    Label = "مكتملة",
                    Value = await jobs.CountAsync(j => j.JobStatusId == (int)JobStatusEnum.Filled),
                    Fill = "var(--chart-4)"
                }
            };

            return Ok(new ApiResponse<ChartPieResponseDTO>(200, new ChartPieResponseDTO
            {
                Title = "توزيع الوظائف",
                Description = "التوزيع الحالي",
                Items = items
            }));
        }

        [Authorize(Roles = "Company,Admin")]
        [HttpGet("dashboard-charts/applications-trend")]
        public async Task<ActionResult<ApiResponse<CompanyApplicationsTrendChartResponseDTO>>> GetApplicationsTrendChart(
            [FromQuery] string period = "weekly")
        {
            if (!string.Equals(period, "weekly", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ApiResponse<string>(400, "Only weekly period is supported."));
            }

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var utcNow = DateTime.UtcNow;
            var startOfWeek = GetStartOfWeek(utcNow);
            var endOfWeek = startOfWeek.AddDays(7);

            var applications = await _context.TbApplications
                .Where(a => a.Job.EmployerId == employerId.Value &&
                            a.ApplicationDate >= startOfWeek &&
                            a.ApplicationDate < endOfWeek)
                .Select(a => a.ApplicationDate)
                .ToListAsync();

            var interviews = await _context.TbInterviews
                .Where(i => i.Application.Job.EmployerId == employerId.Value &&
                            i.InterviewDate >= startOfWeek &&
                            i.InterviewDate < endOfWeek)
                .Select(i => i.InterviewDate)
                .ToListAsync();

            var items = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var day = startOfWeek.AddDays(offset);
                    var nextDay = day.AddDays(1);

                    return new CompanyApplicationsTrendChartPointDTO
                    {
                        Label = WeeklyArabicLabels[offset],
                        Applications = applications.Count(a => a >= day && a < nextDay),
                        Interviews = interviews.Count(i => i >= day && i < nextDay)
                    };
                })
                .ToList();

            return Ok(new ApiResponse<CompanyApplicationsTrendChartResponseDTO>(200, new CompanyApplicationsTrendChartResponseDTO
            {
                Title = "إحصائيات التقديمات",
                Description = "آخر 7 أيام",
                Period = "weekly",
                XKey = "label",
                Series = new List<ChartSeriesDTO>
                {
                    new() { Key = "applications", Label = "التقديمات", Color = "var(--chart-1)" },
                    new() { Key = "interviews", Label = "المقابلات", Color = "var(--chart-2)" }
                },
                Items = items
            }));
        }
    }
}
