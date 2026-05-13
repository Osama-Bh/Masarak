using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.CompanyInterviewDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;

namespace GoWork.Services.InterviewService
{
    public interface IInterviewService
    {
       
        // mobile methods 
        Task<ApiResponse<InterviewResponseDTO>> GetCandidateInterviews(InterviewRequestDTO requestDTO);
        Task<ApiResponse<ConfirmationResponseDTO>> HandleInterviewActionAsync(int interviewId, int userId, InterviewActionDTO dto);


        // company dashboard methods
        Task<ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>> GetCompanyInterviewsAsync(
            int employerId, CompanyInterviewsRequestDTO request);
        Task<ApiResponse<CompanyInterviewFiltersDTO>> GetCompanyInterviewFiltersAsync(int employerId);
        Task<ApiResponse<ConfirmationResponseDTO>> CancelInterviewAsync(int employerId, int interviewId);
        Task<ApiResponse<ConfirmationResponseDTO>> RescheduleInterviewAsync(
            int employerId, int interviewId, RescheduleInterviewRequestDTO dto);
        Task<ApiResponse<ConfirmationResponseDTO>> CompleteInterviewAsync(int employerId, int interviewId);
        Task<ApiResponse<ConfirmationResponseDTO>> MarkMissingInterviewAsync(int employerId, int interviewId);
    }
}
