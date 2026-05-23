using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;

namespace GoWork.Services.ApplicationService
{
    public interface IApplicationService
    {
        // mobile methods 
        public Task<ApiResponse<ApplicationsResponseDTO>> GetCandidateApplications(ApplicationsRequestDTO requestDTO);
        public Task<ApiResponse<List<LookUpDTO>>> GetApplicationStatuses();
        public Task<ApiResponse<ConfirmationResponseDTO>> WithdrawApplicationAsync(int applicationId, int userId);

        // company dashboard methods
        Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyApplicationsAsync(
            int employerId, CompanyApplicationsRequestDTO request);
        Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyEmploymentRecordsAsync(
            int employerId, CompanyApplicationsRequestDTO request);
        Task<ApiResponse<CompanyApplicationFiltersDTO>> GetCompanyApplicationFiltersAsync(int employerId);
        Task<ApiResponse<CompanyApplicationFiltersDTO>> GetCompanyEmploymentRecordFiltersAsync(int employerId);
        Task<ApiResponse<ConfirmationResponseDTO>> RejectApplicationAsync(int employerId, int applicationId);
        Task<ApiResponse<ConfirmationResponseDTO>> HireApplicationAsync(int employerId, int applicationId);
        Task<ApiResponse<ConfirmationResponseDTO>> ScheduleInterviewAsync(
            int employerId, int applicationId, ScheduleInterviewRequestDTO dto);
    }
}
