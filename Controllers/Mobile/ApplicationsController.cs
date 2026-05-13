using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
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

        /// <summary>
        /// Get paginated list of all applications for the company's jobs.
        /// Supports search by job title or candidate name, and filtering by status or job.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>>> GetApplications(
            [FromQuery] CompanyApplicationsRequestDTO request)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            if (request.Page < 1) request.Page = 1;
            if (request.PageSize < 1 || request.PageSize > 50) request.PageSize = 10;

            var response = await _applicationService.GetCompanyApplicationsAsync(employerId.Value, request);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get filter dropdown data (application statuses + company's job titles).
        /// </summary>
        [HttpGet("filters")]
        public async Task<ActionResult<ApiResponse<CompanyApplicationFiltersDTO>>> GetFilters()
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
