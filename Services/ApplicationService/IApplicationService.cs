using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;

namespace GoWork.Services.ApplicationService
{
    public interface IApplicationService
    {
        public Task<ApiResponse<ApplicationsResponseDTO>> GetCandidateApplications(ApplicationsRequestDTO requestDTO);
        public Task<ApiResponse<List<LookUpDTO>>> GetApplicationStatuses();
        public Task<ApiResponse<ConfirmationResponseDTO>> WithdrawApplicationAsync(int applicationId, int userId);

        Task<PaginatedResult<CompanyApplicationDTO>> GetJobApplicationsAsync(int employerUserId, CompanyApplicationsFilterDTO filter);
        Task<bool> UpdateApplicationStatusAsync(int employerUserId, int applicationId, int newStatusId);
        Task<ApiResponse<ConfirmationResponseDTO>> ShortlistApplicationAsync(int employerUserId, int applicationId);
        Task<ApiResponse<ConfirmationResponseDTO>> RejectApplicationAsync(int employerUserId, int applicationId);


    }
}
