using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Services.ApplicationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GoWork.Controllers.Mobile
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        private readonly ApplicationDbContext _context;


        public ApplicationsController(IApplicationService applicationService, ApplicationDbContext context)
        {
            _applicationService = applicationService;
            _context= context;
        }

        [HttpGet]
        [Authorize(Roles = "Candidate, Admin")]
        public async Task<ActionResult<ApiResponse<ApplicationsResponseDTO>>> GetCandidateApplications([FromQuery] ApplicationsRequestDTO requestDTO)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int id))
            {
                return Unauthorized("Unauthorized: User not found.");
            }

            requestDTO.UserId = id;

            var response = await _applicationService.GetCandidateApplications(requestDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpGet("statuses")]
        public async Task<ActionResult<ApiResponse<List<LookUpDTO>>>> GetApplicationStatuses()
        {
            var response = await _applicationService.GetApplicationStatuses();
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpPost("withdraw/{applicationId}")]
        [Authorize(Roles = "Candidate, Admin")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> WithdrawApplication(int applicationId)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int id))
            {
                return Unauthorized("Unauthorized: User not found.");
            }

            var response = await _applicationService.WithdrawApplicationAsync(applicationId, id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }



        // <summary>
        /// Gets the EmployerId from the logged-in user's JWT claim.
        /// </summary>
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

        private IQueryable<GoWork.Models.Application> FilterCompanyActiveApplications(IQueryable<GoWork.Models.Application> query)
        {
            var historicalQuery = FilterCompanyHistoricalApplications(query);
            return query.Where(a => !historicalQuery.Select(h => h.Id).Contains(a.Id));
        }

        /// <summary>
        /// Get paginated list of all applications for the company's jobs.
        /// </summary>
        [HttpGet("company")]
        //[Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>>> GetCompanyApplications(
            [FromQuery] CompanyApplicationsRequestDTO request)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.GetCompanyApplicationsAsync(employerId.Value, request);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get statistics cards for the company's active applications page.
        /// </summary>
        [HttpGet("company/statistics")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyApplicationsStatisticsDTO>>> GetCompanyApplicationsStatistics()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var baseQuery = FilterCompanyActiveApplications(BuildCompanyApplicationsBaseQuery(employerId.Value));
            var utcNow = DateTime.UtcNow;
            var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
            var startOfWeek = utcNow.Date.AddDays(-daysSinceMonday);

            var stats = new CompanyApplicationsStatisticsDTO
            {
                TotalApplications = await baseQuery.CountAsync(),
                PendingReviewApplications = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview),
                ShortlistedApplications = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Shortlisted),
                InterviewedApplications = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed),
                ApplicationsThisWeek = await baseQuery.CountAsync(a => a.ApplicationDate >= startOfWeek)
            };

            return Ok(new ApiResponse<CompanyApplicationsStatisticsDTO>(200, stats));
        }

        /// <summary>
        /// Get paginated list of employment records for the company's jobs.
        /// </summary>
        [HttpGet("company/employment-records")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>>> GetCompanyEmploymentRecords(
            [FromQuery] CompanyApplicationsRequestDTO request)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.GetCompanyEmploymentRecordsAsync(employerId.Value, request);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get statistics cards for the company's employment records page.
        /// </summary>
        [HttpGet("company/employment-records/statistics")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyEmploymentRecordsStatisticsDTO>>> GetCompanyEmploymentRecordsStatistics()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var baseQuery = FilterCompanyHistoricalApplications(BuildCompanyApplicationsBaseQuery(employerId.Value));

            var stats = new CompanyEmploymentRecordsStatisticsDTO
            {
                TotalRecords = await baseQuery.CountAsync(),
                WithdrawnRecords = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Withdrawn),
                MissingInterviewRecords = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.MissingInterview),
                HiredRecords = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Hired),
                RejectedRecords = await baseQuery.CountAsync(a => a.ApplicationStatusId == (int)ApplicationStatusEnum.Rejected)
            };

            return Ok(new ApiResponse<CompanyEmploymentRecordsStatisticsDTO>(200, stats));
        }

        /// <summary>
        /// Get filter dropdown data for employment records (historical statuses + company's job titles).
        /// </summary>
        [HttpGet("company/employment-records/filters")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyApplicationFiltersDTO>>> GetCompanyEmploymentRecordFilters()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.GetCompanyEmploymentRecordFiltersAsync(employerId.Value);
            return Ok(response);
        }


        /// <summary>
        /// Get filter dropdown data (application statuses + company's job titles).
        /// </summary>
        [HttpGet("company/filters")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyApplicationFiltersDTO>>> GetCompanyFilters()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.GetCompanyApplicationFiltersAsync(employerId.Value);
            return Ok(response);
        }
        /// <summary>
        /// Reject an application. Allowed from PendingReview, Shortlisted, or Interviewed status.
        /// </summary>
        [HttpPost("{id}/reject")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> RejectApplication(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.RejectApplicationAsync(employerId.Value, id);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Hire a candidate. Allowed only from Interviewed status.
        /// </summary>
        [HttpPost("{id}/hire")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> HireApplication(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.HireApplicationAsync(employerId.Value, id);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Schedule an interview for an application. Allowed only from PendingReview status.
        /// Creates an Interview record and changes application status to Interview.
        /// </summary>
        [HttpPost("{id}/schedule-interview")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ScheduleInterview(
            int id, [FromBody] ScheduleInterviewRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<string>(400, "Invalid request data."));

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _applicationService.ScheduleInterviewAsync(employerId.Value, id, dto);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }
    }
}
