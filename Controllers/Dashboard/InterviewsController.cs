using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;
using GoWork.Services.InterviewService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoWork.Controllers.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class InterviewsController : ControllerBase
    {
        private readonly IInterviewService _interviewService;

        public InterviewsController(IInterviewService interviewService)
        {
            _interviewService = interviewService;
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

        [HttpGet]
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
        /// Get interview statistics for dashboard stats cards.
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponse<InterviewStatisticsDTO>>> GetInterviewStatistics()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _interviewService.GetInterviewStatisticsAsync(userId);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get paginated list of interviews with search and filter support.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyInterviewDTO>>>> GetInterviews(
            [FromQuery] InterviewFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1 || filter.PageSize > 50) filter.PageSize = 10;

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _interviewService.GetInterviewsAsync(userId, filter);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get detailed information for a single interview.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<CompanyInterviewDTO>>> GetInterviewById(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _interviewService.GetInterviewByIdAsync(userId, id);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Update interview status (Confirm, Complete, Cancel, NoShow).
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateInterviewStatus(
            int id, UpdateInterviewStatusDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _interviewService.UpdateInterviewStatusAsync(userId, id, dto);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Reschedule an interview with a new date and optional notes.
        /// </summary>
        [HttpPost("{id}/reschedule")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> RescheduleInterview(
            int id, RescheduleInterviewDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await _interviewService.RescheduleInterviewAsync(userId, id, dto);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get all active interview statuses for dropdown/filter.
        /// </summary>
        [HttpGet("statuses")]
        public async Task<ActionResult<ApiResponse<List<LookUpDTO>>>> GetInterviewStatuses()
        {
            var response = await _interviewService.GetInterviewStatusesAsync();

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }
    }
}
