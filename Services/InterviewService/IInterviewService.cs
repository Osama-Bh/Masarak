using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;

namespace GoWork.Services.InterviewService
{
    public interface IInterviewService
    {
        Task<ApiResponse<InterviewStatisticsDTO>> GetInterviewStatisticsAsync(int employerUserId);

        Task<ApiResponse<PaginatedResult<CompanyInterviewDTO>>> GetInterviewsAsync(
            int employerUserId, InterviewFilterDTO filter);

        Task<ApiResponse<CompanyInterviewDTO>> GetInterviewByIdAsync(
            int employerUserId, int interviewId);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateInterviewStatusAsync(
            int employerUserId, int interviewId, UpdateInterviewStatusDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> RescheduleInterviewAsync(
            int employerUserId, int interviewId, RescheduleInterviewDTO dto);

        Task<ApiResponse<List<LookUpDTO>>> GetInterviewStatusesAsync();

        Task<ApiResponse<InterviewResponseDTO>> GetCandidateInterviews(InterviewRequestDTO requestDTO);
        Task<ApiResponse<ConfirmationResponseDTO>> HandleInterviewActionAsync(int interviewId, int userId, InterviewActionDTO dto);

    }
}
