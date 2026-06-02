using System.Threading.Tasks;
using GoWork.Enums;
using GoWork.DTOs.NotificationDTOs;
using GoWork.DTOs.DashboardDTOs;
using ECommerceApp.DTOs;

namespace GoWork.Services.NotificationService
{
    public interface INotificationService
    {
        // --- Sending ---
        Task SendToTopicAsync(string topic, string title, string body,
            NotificationTypeEnum type, string? actionUrl = null, string? imageUrl = null);

        Task SendToUserAsync(int userId, string title, string body,
            NotificationTypeEnum type, string? actionUrl = null, string? imageUrl = null);

        // --- User notification queries ---
        Task<ApiResponse<PaginatedResult<NotificationResponseDTO>>> GetUserNotificationsAsync(
            int userId, int pageNumber = 1, int pageSize = 20);

        Task<ApiResponse<UnreadCountDTO>> GetUnreadCountAsync(int userId);

        Task<ApiResponse<ConfirmationResponseDTO>> MarkAsReadAsync(int userId, int userNotificationId);

        Task<ApiResponse<ConfirmationResponseDTO>> MarkAllAsReadAsync(int userId);

        Task<ApiResponse<ConfirmationResponseDTO>> HideNotificationAsync(int userId, int userNotificationId);

        // --- Device tokens ---
        Task<ApiResponse<ConfirmationResponseDTO>> RegisterDeviceTokenAsync(
            int userId, RegisterDeviceTokenDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> RemoveDeviceTokenAsync(
            int userId, string token);
    }
}
