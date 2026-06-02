using System;
using GoWork.Data;

namespace GoWork.Models
{
    public class UserNotification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
        public int NotificationId { get; set; }
        public Notification Notification { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsHidden { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }
}
