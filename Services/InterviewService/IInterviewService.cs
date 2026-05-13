using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;

namespace GoWork.Services.InterviewService
{
    public interface IInterviewService
    {
       
        // mobile methods 
        Task<ApiResponse<InterviewResponseDTO>> GetCandidateInterviews(InterviewRequestDTO requestDTO);
        Task<ApiResponse<ConfirmationResponseDTO>> HandleInterviewActionAsync(int interviewId, int userId, InterviewActionDTO dto);

    }
}
