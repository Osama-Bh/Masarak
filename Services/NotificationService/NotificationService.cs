using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GoWork.Data;
using GoWork.Models;
using GoWork.Enums;
using GoWork.DTOs.NotificationDTOs;
using GoWork.DTOs.DashboardDTOs;
using ECommerceApp.DTOs;
using GoWork.Services.FirebaseNotificationSender;

namespace GoWork.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFirebaseNotificationSender _firebaseNotificationSender;

        public NotificationService(
            ApplicationDbContext context,
            IFirebaseNotificationSender firebaseNotificationSender)
        {
            _context = context;
            _firebaseNotificationSender = firebaseNotificationSender;
        }

        public async Task SendToTopicAsync(string topic, string title, string body,
            NotificationTypeEnum type, string? actionUrl = null, string? imageUrl = null)
        {
            // Parse topic (format: categoryName_{id})
            int? categoryId = null;
            if (!string.IsNullOrEmpty(topic) && topic.Contains('_'))
            {
                var parts = topic.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out var id))
                {
                    categoryId = id;
                }
            }

            // Get all seekers interested in this category
            var recipientUserIds = new List<int>();
            if (categoryId.HasValue)
            {
                recipientUserIds = await _context.TbSeekers
                    .Where(s => s.InterestCategoryId == categoryId.Value)
                    .Select(s => s.UserId)
                    .ToListAsync();
            }

            // Create notification record
            var notification = new Notification
            {
                Title = title,
                Body = body,
                Type = type,
                DeliveryType = NotificationDeliveryTypeEnum.Topic,
                Topic = topic,
                ActionUrl = actionUrl,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.TbNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // Create user notifications history if we have recipients
            if (recipientUserIds.Any())
            {
                var userNotifications = recipientUserIds.Select(userId => new UserNotification
                {
                    UserId = userId,
                    NotificationId = notification.Id,
                    IsRead = false,
                    IsHidden = false,
                    DeliveredAt = DateTime.UtcNow
                }).ToList();

                _context.TbUserNotifications.AddRange(userNotifications);
                await _context.SaveChangesAsync();
            }

            // Send via Firebase topic messaging - Firebase handles fan-out to all subscribed devices
            var data = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(actionUrl)) data.Add("actionUrl", actionUrl);
            if (!string.IsNullOrEmpty(imageUrl)) data.Add("imageUrl", imageUrl);
            data.Add("type", type.ToString());

            await _firebaseNotificationSender.SendToTopicAsync(topic, title, body, data);
        }

        public async Task SendToUserAsync(int userId, string title, string body,
            NotificationTypeEnum type, string? actionUrl = null, string? imageUrl = null)
        {
            // Create notification record
            var notification = new Notification
            {
                Title = title,
                Body = body,
                Type = type,
                DeliveryType = NotificationDeliveryTypeEnum.User,
                ActionUrl = actionUrl,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.TbNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // Create user notification history record
            var userNotification = new UserNotification
            {
                UserId = userId,
                NotificationId = notification.Id,
                IsRead = false,
                IsHidden = false,
                DeliveredAt = DateTime.UtcNow
            };

            _context.TbUserNotifications.Add(userNotification);
            await _context.SaveChangesAsync();

            // Retrieve all active device tokens for the user
            var tokens = await _context.TbDeviceTokens
                .Where(dt => dt.UserId == userId)
                .Select(dt => dt.Token)
                .ToListAsync();

            if (tokens.Any())
            {
                var userData = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(actionUrl)) userData.Add("actionUrl", actionUrl);
                if (!string.IsNullOrEmpty(imageUrl)) userData.Add("imageUrl", imageUrl);
                userData.Add("type", type.ToString());

                // Send multicast messages to all of user's active device tokens
                await _firebaseNotificationSender.SendToTokensAsync(tokens, title, body, userData);
            }
        }

        public async Task<ApiResponse<PaginatedResult<NotificationResponseDTO>>> GetUserNotificationsAsync(
            int userId, int pageNumber = 1, int pageSize = 20)
        {
            var query = _context.TbUserNotifications
                .Include(un => un.Notification)
                .Where(un => un.UserId == userId && !un.IsHidden);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(un => un.Notification.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(un => new NotificationResponseDTO
                {
                    Id = un.Id,
                    NotificationId = un.NotificationId,
                    Title = un.Notification.Title,
                    Body = un.Notification.Body,
                    Type = un.Notification.Type,
                    DeliveryType = un.Notification.DeliveryType,
                    CreatedAt = un.Notification.CreatedAt,
                    IsRead = un.IsRead,
                    ActionUrl = un.Notification.ActionUrl,
                    ImageUrl = un.Notification.ImageUrl
                })
                .ToListAsync();

            var paginatedResult = new PaginatedResult<NotificationResponseDTO>
            {
                Items = items,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return new ApiResponse<PaginatedResult<NotificationResponseDTO>>(200, paginatedResult);
        }

        public async Task<ApiResponse<UnreadCountDTO>> GetUnreadCountAsync(int userId)
        {
            var count = await _context.TbUserNotifications
                .CountAsync(un => un.UserId == userId && !un.IsHidden && !un.IsRead);

            return new ApiResponse<UnreadCountDTO>(200, new UnreadCountDTO { Count = count });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkAsReadAsync(int userId, int userNotificationId)
        {
            var userNotification = await _context.TbUserNotifications
                .FirstOrDefaultAsync(un => un.Id == userNotificationId && un.UserId == userId);

            if (userNotification == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Notification not found.");
            }

            userNotification.IsRead = true;
            userNotification.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Notification marked as read."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.TbUserNotifications
                .Where(un => un.UserId == userId && !un.IsRead && !un.IsHidden)
                .ToListAsync();

            foreach (var un in unreadNotifications)
            {
                un.IsRead = true;
                un.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "All notifications marked as read."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> HideNotificationAsync(int userId, int userNotificationId)
        {
            var userNotification = await _context.TbUserNotifications
                .FirstOrDefaultAsync(un => un.Id == userNotificationId && un.UserId == userId);

            if (userNotification == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Notification not found.");
            }

            userNotification.IsHidden = true;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Notification hidden."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RegisterDeviceTokenAsync(
            int userId, RegisterDeviceTokenDTO dto)
        {
            var existingToken = await _context.TbDeviceTokens
                .FirstOrDefaultAsync(dt => dt.Token == dto.Token);

            if (existingToken != null)
            {
                if (existingToken.UserId != userId || existingToken.DeviceType != dto.DeviceType)
                {
                    existingToken.UserId = userId;
                    existingToken.DeviceType = dto.DeviceType;
                    existingToken.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var deviceToken = new DeviceToken
                {
                    UserId = userId,
                    Token = dto.Token,
                    DeviceType = dto.DeviceType,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TbDeviceTokens.Add(deviceToken);
                await _context.SaveChangesAsync();
            }

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Device token registered."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RemoveDeviceTokenAsync(int userId, string token)
        {
            var deviceToken = await _context.TbDeviceTokens
                .FirstOrDefaultAsync(dt => dt.UserId == userId && dt.Token == token);

            if (deviceToken != null)
            {
                _context.TbDeviceTokens.Remove(deviceToken);
                await _context.SaveChangesAsync();
            }

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Device token removed."
            });
        }
    }
}
