using System;
using System.Collections.Generic;
using GoWork.Enums;

namespace GoWork.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public NotificationTypeEnum Type { get; set; }
        public NotificationDeliveryTypeEnum DeliveryType { get; set; }
        public string? Topic { get; set; }
        public string? ActionUrl { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    }
}
