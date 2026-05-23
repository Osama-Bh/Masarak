using ECommerceApp.DTOs;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.FeedbackDTOs;

namespace GoWork.Services.FeedbackService
{
    public interface IFeedbackService
    {
        Task<ApiResponse<ConfirmationResponseDTO>> SubmitFeedbackAsync(int userId, SubmitFeedbackDTO dto);
        Task<ApiResponse<FeedbackStatisticsDTO>> GetFeedbackStatisticsAsync();
        Task<ApiResponse<PaginatedResult<FeedbackResponseDTO>>> GetAllFeedbacksAsync(int? feedbackTypeId = null, bool? isRead = null, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<LookUpDTO>>> GetFeedbackTypesAsync();
        Task<ApiResponse<ConfirmationResponseDTO>> MarkAsReadAsync(int feedbackId);
        Task<ApiResponse<ConfirmationResponseDTO>> DeleteFeedbackAsync(int feedbackId);
        Task<ApiResponse<ConfirmationResponseDTO>> SendEmailReplyAsync(SendEmailRequestDTO dto);
    }
}
