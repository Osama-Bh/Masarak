using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.JobDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.Enums;
using GoWork.Services.JobService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GoWork.Controllers.JobController
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Candidate,Company,Admin")]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly ApplicationDbContext _context;

        public JobsController(IJobService jobService, ApplicationDbContext context)
        {
            _jobService = jobService;
            _context = context;
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

        /// <summary>
        /// Gets the SeekerId from the logged-in user's JWT claim.
        /// </summary>
        private async Task<int?> GetSeekerIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return null;

            var seeker = await _context.TbSeekers.FirstOrDefaultAsync(s => s.UserId == userId);
            return seeker?.Id;
        }

        // ==================== Job CRUD ====================

        /// <summary>
        /// Get statistics cards for the logged-in company's jobs page.
        /// </summary>
        [HttpGet("jobs/statistics")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<CompanyJobsStatisticsDTO>>> GetJobsStatistics()
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.GetJobsStatisticsAsync(employerId.Value);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get paginated list of the logged-in company's jobs.
        /// </summary>
        [HttpGet("jobs")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<JobListItemDTO>>>> GetJobs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int? jobTypeId = null)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var response = await _jobService.GetJobsAsync(employerId.Value, page, pageSize, search, status, jobTypeId);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get detailed information for a single job.
        /// </summary>
        [HttpGet("jobs/{id}")]
        public async Task<ActionResult<ApiResponse<JobDetailDTO>>> GetJobById(int id)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.GetJobByIdAsync(employerId.Value, id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Create a new job posting.
        /// </summary>
        [HttpPost("jobs")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> CreateJob(CreateJobDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid job data.");

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.CreateJobAsync(employerId.Value, dto);
            if (response.StatusCode != 201)
                return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        /// <summary>
        /// Enhance a job description using AI.
        /// </summary>
        [HttpPost("enhance-description")]
        [Authorize(Roles = "Company")]
        public async Task<ActionResult<ApiResponse<JobDescriptionEnhancementResultDTO>>> EnhanceDescription(EnhanceJobDescriptionDTO dto)
        {
            var response = await _jobService.EnhanceJobDescriptionAsync(dto);
            if (response.StatusCode == 200)
                return Ok(response);

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Update an existing job.
        /// </summary>
        [HttpPut("jobs/{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateJob(int id, UpdateJobDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid job data.");

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.UpdateJobAsync(employerId.Value, id, dto);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        /// <summary>
        /// Update job status (Published / Closed).
        /// </summary>
        [HttpPatch("jobs/{id}/status")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateJobStatus(int id, UpdateJobStatusDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid request data.");

            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.UpdateJobStatusAsync(employerId.Value, id, dto);
            if (response.StatusCode != 200)
                return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        /// <summary>
        /// Get AI-powered job recommendations for the logged-in seeker.
        /// </summary>
        [HttpGet("recommendations")]
        [Authorize(Roles = "Candidate,Company,Admin")]
        public async Task<ActionResult<ApiResponse<JobRecommendationResultDto>>> GetJobRecommendations()
        {
            var seekerId = await GetSeekerIdAsync();
            if (seekerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Access denied. Only registered job seekers can perform this action."));

            var response = await _jobService.GetJobRecommendationsAsync(seekerId.Value);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Search and filter jobs using pagination.
        /// Empty queries default to the Candidate's interest category if logged in.
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<JobSearchResponseDto>>> SearchJobs([FromQuery] JobSearchRequestDto request)
        {
            // Attach seeker ID if the user is authenticated as Candidate
            var seekerId = await GetSeekerIdAsync();
            if (seekerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Access denied. Only registered job seekers can perform this action."));

            request.SeekerId = seekerId;

            var response = await _jobService.SearchJobsAsync(request);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get detailed information for a single job for seekers.
        /// </summary>
        [HttpGet("{jobId}")]
        public async Task<ActionResult<ApiResponse<JobDetailsDto>>> GetJobDetails(int jobId)
        {
            var seekerId = await GetSeekerIdAsync();
            if (seekerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Access denied. Only registered job seekers can perform this action."));

            var response = await _jobService.GetJobDetailsAsync(jobId, seekerId);
            
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Applies the logged-in Candidate to the specified job.
        /// </summary>
        [HttpPost("{jobId}/apply")]
        public async Task<ActionResult<ApiResponse<ApplicationResultDto>>> ApplyToJob(int jobId)
        {
            var seekerId = await GetSeekerIdAsync();
            if (seekerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Access denied. Only registered job seekers can perform this action."));

            var response = await _jobService.ApplyToJobAsync(jobId, seekerId.Value);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        // ==================== Job Applicants ====================

        /// <summary>
        /// Get paginated list of applicants for a specific job.
        /// </summary>
        [HttpGet("{jobId}/applicants")]
        [Authorize(Roles = "Company,Admin")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<JobApplicantDTO>>>> GetJobApplicants(
            int jobId, [FromQuery] CompanyApplicationsRequestDTO request)
        {
            var employerId = await GetEmployerIdAsync();
            if (employerId == null)
                return Unauthorized(new ApiResponse<string>(401, "Company profile not found."));

            var response = await _jobService.GetJobApplicantsAsync(employerId.Value, jobId, request);
            
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        // ==================== Lookup Endpoints (for combo boxes) ====================

        /// <summary>
        /// Get all job categories.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponse<List<LookupDTO>>>> GetCategories([FromQuery] string? search = null)
        {
            var response = await _jobService.GetCategoriesAsync(search);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get all job types (FullTime, PartTime).
        /// </summary>
        [HttpGet("job-types")]
        public async Task<ActionResult<ApiResponse<List<LookupDTO>>>> GetJobTypes()
        {
            var response = await _jobService.GetJobTypesAsync();
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get all location types (OnSite, Remote, Hybrid).
        /// </summary>
        [HttpGet("location-types")]
        public async Task<ActionResult<ApiResponse<List<LookupDTO>>>> GetLocationTypes()
        {
            var response = await _jobService.GetLocationTypesAsync();
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get all currencies.
        /// </summary>
        [HttpGet("currencies")]
        public async Task<ActionResult<ApiResponse<List<CurrencyLookupDTO>>>> GetCurrencies([FromQuery] string? search = null)
        {
            var response = await _jobService.GetCurrenciesAsync(search);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get all countries.
        /// </summary>
        [HttpGet("countries")]
        public async Task<ActionResult<ApiResponse<List<CountryLookupDTO>>>> GetCountries([FromQuery] string? search = null)
        {
            var response = await _jobService.GetCountriesAsync(search);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get governates/regions for a specific country.
        /// </summary>
        [HttpGet("governates/{countryId}")]
        public async Task<ActionResult<ApiResponse<List<LookupDTO>>>> GetGovernates(int countryId, [FromQuery] string? search = null)
        {
            var response = await _jobService.GetGovernatesAsync(countryId, search);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Search skills (for combo box, returns max 50 results).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("skills")]
        public async Task<ActionResult<ApiResponse<List<SkillDTO>>>> GetSkills([FromQuery] string? search = null)
        {
            var response = await _jobService.GetSkillsAsync(search);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }
    }
}
