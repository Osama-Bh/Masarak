using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.DTOs;
using GoWork.DTOs.NotificationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Services.NotificationService;
using GoWork.Services.CurrentUserService;

namespace GoWork.Controllers.Mobile
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize(Roles = "Candidate,Company,Admin")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;

        public NotificationsController(
            INotificationService notificationService,
            ICurrentUserService currentUserService)
        {
            _notificationService = notificationService;
            _currentUserService = currentUserService;
        }

        // GET /api/notifications
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResult<NotificationResponseDTO>>>> GetNotifications(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<PaginatedResult<NotificationResponseDTO>>(401, "User is unauthorized."));
            }

            var response = await _notificationService.GetUserNotificationsAsync(userId, pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        // GET /api/notifications/unread-count
        [HttpGet("unread")]
        public async Task<ActionResult<ApiResponse<UnreadCountDTO>>> GetUnreadCount()
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<UnreadCountDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.GetUnreadCountAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        // PUT /api/notifications/{id}/read
        [HttpPost("{id}/read")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> MarkAsRead(int id)
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<ConfirmationResponseDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.MarkAsReadAsync(userId, id);
            return StatusCode(response.StatusCode, response);
        }

        // PUT /api/notifications/read-all
        [HttpPost("read-all")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> MarkAllAsRead()
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<ConfirmationResponseDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.MarkAllAsReadAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE /api/notifications/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> HideNotification(int id)
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<ConfirmationResponseDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.HideNotificationAsync(userId, id);
            return StatusCode(response.StatusCode, response);
        }

        // POST /api/notifications/device-tokens
        [HttpPost("device-tokens")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> RegisterDeviceToken(
            [FromBody] RegisterDeviceTokenDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<ConfirmationResponseDTO>(400, "Invalid request body."));
            }

            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<ConfirmationResponseDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.RegisterDeviceTokenAsync(userId, dto);
            return StatusCode(response.StatusCode, response);
        }

        // DELETE /api/notifications/device-tokens/{token}
        [HttpDelete("device-tokens/{token}")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> RemoveDeviceToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ApiResponse<ConfirmationResponseDTO>(400, "Token is required."));
            }

            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                return Unauthorized(new ApiResponse<ConfirmationResponseDTO>(401, "User is unauthorized."));
            }

            var response = await _notificationService.RemoveDeviceTokenAsync(userId, token);
            return StatusCode(response.StatusCode, response);
        }
    }
}
