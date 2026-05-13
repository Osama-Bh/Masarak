using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;

namespace GoWork.Services.ApplicationService
{
    public interface IApplicationService
    {
        // mobile methods 
        public Task<ApiResponse<ApplicationsResponseDTO>> GetCandidateApplications(ApplicationsRequestDTO requestDTO);
        public Task<ApiResponse<List<LookUpDTO>>> GetApplicationStatuses();
        public Task<ApiResponse<ConfirmationResponseDTO>> WithdrawApplicationAsync(int applicationId, int userId);

    }
}
