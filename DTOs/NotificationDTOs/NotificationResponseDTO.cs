using System;
using GoWork.Enums;

namespace GoWork.DTOs.NotificationDTOs
{
    public class NotificationResponseDTO
    {
        public int Id { get; set; }              // UserNotification.Id
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public NotificationTypeEnum Type { get; set; }
        public NotificationDeliveryTypeEnum DeliveryType { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
        public string? ImageUrl { get; set; }
    }
}
