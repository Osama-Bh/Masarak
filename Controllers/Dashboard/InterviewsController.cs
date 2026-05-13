using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.CompanyInterviewDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;
using GoWork.Services.InterviewService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GoWork.Controllers.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin, Company")]

    public class InterviewsController : ControllerBase
    {
        private readonly IInterviewService _interviewService;
        private readonly ApplicationDbContext _context;

        public InterviewsController(IInterviewService interviewService,ApplicationDbContext context)
        {
            _interviewService = interviewService;
            _context = context;
        }

        [HttpPost("{id}/action")]
        [Authorize(Roles = "Candidate, Admin")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> HandleInterviewAction(int id, InterviewActionDTO dto)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int userId))
            {
                return Unauthorized("Unauthorized: User not found.");
            }

            var response = await _interviewService.HandleInterviewActionAsync(id, userId, dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpGet("CandidateInterviews")]
        [Authorize(Roles = "Candidate, Admin")]
        public async Task<ActionResult<ApiResponse<InterviewResponseDTO>>> GetCandidateInterviews([FromQuery] InterviewRequestDTO requestDTO)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int id))
            {
                return Unauthorized("Unauthorized: User not found.");
            }

            requestDTO.UserId = id;

            var response = await _interviewService.GetCandidateInterviews(requestDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        /// <summary>
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

        ///// <summary>
        ///// Get paginated list of all interviews for the company's jobs.
        ///// Supports search by candidate name or job title, and filtering by status or job.
        ///// </summary>
        //[HttpGet]
        //public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>>> GetInterviews(
        //    [FromQuery] CompanyInterviewsRequestDTO request)
        //{
        //    var employerId = await GetEmployerIdAsync();
        //    if (employerId == null)
        //        return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

        //    if (request.Page < 1) request.Page = 1;
        //    if (request.PageSize < 1 || request.PageSize > 50) request.PageSize = 10;

        //    var response = await _interviewService.GetCompanyInterviewsAsync(employerId.Value, request);
        //    if (response.StatusCode != 200)
        //        return StatusCode(response.StatusCode, response);

        //    return Ok(response);
        //}

        /// <summary>
        /// Get paginated list of all interviews for the company's jobs.
        /// </summary>
        [HttpGet("company")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>>> GetCompanyInterviews(
            [FromQuery] CompanyInterviewsRequestDTO request)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.GetCompanyInterviewsAsync(employerId.Value, request);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get filter dropdown data (interview statuses + company's job titles).
        /// </summary>
        [HttpGet("company/filters")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyInterviewFiltersDTO>>> GetCompanyFilters()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.GetCompanyInterviewFiltersAsync(employerId.Value);
            return Ok(response);
        }

        /// <summary>
        /// Cancel an interview. Allowed from Scheduled or Confirmed (future date) status.
        /// This also rejects the associated application.
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> CancelInterview(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.CancelInterviewAsync(employerId.Value, id);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Reschedule an interview. Allowed only from Scheduled status.
        /// Updates date, type, and location. Status stays Scheduled for re-confirmation.
        /// </summary>
        [HttpPost("{id}/reschedule")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> RescheduleInterview(
            int id, [FromBody] RescheduleInterviewRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<string>(400, "Invalid request data."));

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.RescheduleInterviewAsync(employerId.Value, id, dto);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Mark an interview as completed. Allowed only when Confirmed and date is today.
        /// This also updates the application status to Interviewed.
        /// </summary>
        [HttpPost("{id}/complete")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> CompleteInterview(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.CompleteInterviewAsync(employerId.Value, id);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Mark an interview as missing (candidate did not attend).
        /// Allowed only when Confirmed and date is today.
        /// This also updates the application status to MissingInterview.
        /// </summary>
        [HttpPost("{id}/missing")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> MarkMissingInterview(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _interviewService.MarkMissingInterviewAsync(employerId.Value, id);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }
    }
}
