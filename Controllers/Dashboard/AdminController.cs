using ECommerceApp.DTOs;
using GoWork.DTOs.AuthDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Services.AdminService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoWork.Controllers.Dashboard
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SubAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet("dashboard-charts/company-status-distribution")]
        public async Task<ActionResult<ApiResponse<ChartPieResponseDTO>>> GetCompanyStatusDistributionChart()
        {
            var response = await _adminService.GetAdminCompanyStatusDistributionChartAsync();
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpGet("dashboard-charts/company-registrations")]
        public async Task<ActionResult<ApiResponse<AdminCompanyRegistrationsChartResponseDTO>>> GetCompanyRegistrationsChart(
            [FromQuery] string period = "weekly")
        {
            if (!string.Equals(period, "weekly", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ApiResponse<string>(400, "Only weekly period is supported."));
            }

            var response = await _adminService.GetAdminCompanyRegistrationsChartAsync();
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get combined dashboard statistics for shared Admin/SubAdmin dashboard cards.
        /// </summary>
        [HttpGet("dashboard-statistics")]
        public async Task<ActionResult<ApiResponse<AdminDashboardStatisticsDTO>>> GetDashboardStatistics()
        {
            var response = await _adminService.GetAdminDashboardStatisticsAsync();

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get company statistics for dashboard cards.
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponse<CompanyStatisticsDTO>>> GetStatistics()
        {
            var response = await _adminService.GetCompanyStatisticsAsync();

            if(response.StatusCode!=200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get paginated list of companies with search and filter support.
        /// </summary>
        [HttpGet("companies")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<CompanyListItemDTO>>>> GetCompanies(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var response = await _adminService.GetCompaniesAsync(page, pageSize, search, status, sortBy, sortOrder);

            if(response.StatusCode!=200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Get detailed information for a single company.
        /// </summary>
        [HttpGet("companies/{id}")]
        public async Task<ActionResult<ApiResponse<CompanyDetailDTO>>> GetCompanyById(int id)
        {
            var response = await _adminService.GetCompanyByIdAsync(id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Update company status (Approve / Reject / Suspend).
        /// </summary>
        [HttpPatch("companies/{id}/status")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateCompanyStatus(int id, UpdateCompanyStatusDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _adminService.UpdateCompanyStatusAsync(id, dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Soft delete a company (sets status to Suspended).
        /// </summary>
        [HttpDelete("companies/{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> DeleteCompany(int id)
        {
            var response = await _adminService.DeleteCompanyAsync(id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Update company details (name, industry, email, phone).
        /// </summary>
        [HttpPut("companies/{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateCompany(int id, AdminUpdateCompanyDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _adminService.UpdateCompanyAsync(id, dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Perform bulk actions on multiple companies (Approve, Reject, Suspend, Delete).
        /// </summary>
        [HttpPost("companies/bulk-action")]
        public async Task<ActionResult<ApiResponse<BulkActionResultDTO>>> BulkAction(BulkActionDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _adminService.BulkActionAsync(dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        // ==================== Sub-Admin Management (Admin only) ====================

        /// <summary>
        /// Get sub-admin statistics for dashboard cards.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("sub-admins/statistics")]
        public async Task<ActionResult<ApiResponse<SubAdminStatisticsDTO>>> GetSubAdminStatistics()
        {
            var response = await _adminService.GetSubAdminStatisticsAsync();
            return Ok(response);
        }

        /// <summary>
        /// Get paginated list of sub-admins with search and filter.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("sub-admins")]
        public async Task<ActionResult<ApiResponse<PaginatedResult<SubAdminListItemDTO>>>> GetSubAdmins(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;

            var response = await _adminService.GetSubAdminsAsync(page, pageSize, search, status);
            return Ok(response);
        }

        /// <summary>
        /// Get detailed information for a single sub-admin.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("sub-admins/{id}")]
        public async Task<ActionResult<ApiResponse<SubAdminDetailDTO>>> GetSubAdminById(int id)
        {
            var response = await _adminService.GetSubAdminByIdAsync(id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Update sub-admin status (Activate / Suspend).
        /// </summary>
        [Authorize(Roles = "Admin")]
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

        /// <summary>
        /// Soft delete a sub-admin (sets status to Blocked).
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("sub-admins/{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> DeleteSubAdmin(int id)
        {
            var response = await _adminService.DeleteSubAdminAsync(id);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Update sub-admin details (name, email, phone).
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("sub-admins/{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateSubAdmin(int id, UpdateSubAdminDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await _adminService.UpdateSubAdminAsync(id, dto);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }

        /// <summary>
        /// Create a new sub-admin.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("sub-admins")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> CreateSubAdmin(AdminRegistrationDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid registration data.");
            }

            var response = await _adminService.CreateSubAdminAsync(dto);
            if (response.StatusCode != 200 && response.StatusCode != 201)
            {
                return StatusCode(response.StatusCode, response);
            }
            return Ok(response);
        }
    }
}
