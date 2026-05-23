using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.FeedbackDTOs;
using GoWork.Services.FeedbackService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoWork.Controllers.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        /// <summary>
        /// Submit a new feedback message (FeatureRequest or Complaint).
        /// </summary>
        /// <remarks>
        /// The authenticated user's ID is used as the ReviewerId.
        /// FeedbackType values: 1 = FeatureRequest, 2 = Complaint.
        /// </remarks>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> SubmitFeedback(
            [FromBody] SubmitFeedbackDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<ConfirmationResponseDTO>(400, "Invalid request data."));

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var response = await _feedbackService.SubmitFeedbackAsync(userId, dto);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get all feedback entries (Admin only).
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "Admin,SubAdmin")]
        public async Task<ActionResult<ApiResponse<FeedbackStatisticsDTO>>> GetStatistics()
        {
            var response = await _feedbackService.GetFeedbackStatisticsAsync();

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Get all feedback entries (Admin only).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,SubAdmin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<FeedbackResponseDTO>>>> GetFeedbacks(
            [FromQuery] int? feedbackTypeId,
            [FromQuery] bool? isRead,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _feedbackService.GetAllFeedbacksAsync(feedbackTypeId, isRead, pageNumber, pageSize);
            return Ok(response);
        }

        [HttpGet("types")]
        public async Task<ActionResult<ApiResponse<List<LookUpDTO>>>> GetFeedbackTypes()
        {
            var response = await _feedbackService.GetFeedbackTypesAsync();
            return Ok(response);
        }

        /// <summary>
        /// Mark a feedback as read (Admin only).
        /// </summary>
        [HttpPatch("{id}/read")]
        [Authorize(Roles = "Admin,SubAdmin")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> MarkAsRead(int id)
        {
            var response = await _feedbackService.MarkAsReadAsync(id);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Delete a feedback entry (Admin only).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SubAdmin")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> DeleteFeedback(int id)
        {
            var response = await _feedbackService.DeleteFeedbackAsync(id);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        /// <summary>
        /// Send an email reply to a user (Admin only).
        /// </summary>
        [HttpPost("send-reply")]
        [Authorize(Roles = "Admin,SubAdmin")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> SendReply(
            [FromBody] SendEmailRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<ConfirmationResponseDTO>(400, "Invalid request data."));

            var response = await _feedbackService.SendEmailReplyAsync(dto);

            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }
    }
}
