using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Services.ApplicationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoWork.Controllers.Mobile
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _applicationService;
        

        public ApplicationsController(IApplicationService applicationService)
        {
            _applicationService = applicationService;
            
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

        //--------

        /// <summary>
        /// Retrieves a paginated list of job applications for the currently logged-in company (Employer).
        /// Supports filtering by SearchTerm (Name/Email), JobId, and StatusId.
        /// </summary>
        [Authorize(Roles = "Company")]
        [HttpGet("applications")]
        public async Task<IActionResult> GetJobApplications([FromQuery] CompanyApplicationsFilterDTO filter)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new { Message = "User ID not found or invalid." });
            }

            try
            {
                var result = await _applicationService.GetJobApplicationsAsync(userId, filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Updates the status of a specific job application (e.g., Accept or Reject).
        /// Only the company that owns the job can update the application status.
        /// </summary>
        [Authorize(Roles = "Company")]
        [HttpPut("applications/{applicationId}/status")]
        public async Task<IActionResult> UpdateApplicationStatus(int applicationId, [FromBody] UpdateApplicationStatusDTO dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new { Message = "User ID not found or invalid." });
            }

            var success = await _applicationService.UpdateApplicationStatusAsync(userId, applicationId, dto.StatusId);

            if (!success)
            {
                return BadRequest(new { Message = "Failed to update status. Application not found, not authorized, or invalid status ID." });
            }

            return Ok(new { Message = "Application status updated successfully." });
        }

        [Authorize(Roles = "Company")]
        [HttpPost("{applicationId}/shortlist")]
        public async Task<IActionResult> ShortlistApplication(int applicationId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "User ID not found or invalid." });

            var response = await _applicationService.ShortlistApplicationAsync(userId, applicationId);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        [Authorize(Roles = "Company")]
        [HttpPost("{applicationId}/reject")]
        public async Task<IActionResult> RejectApplication(int applicationId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Unauthorized(new { Message = "User ID not found or invalid." });

            var response = await _applicationService.RejectApplicationAsync(userId, applicationId);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }
    }
}
