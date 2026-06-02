using System;
using GoWork.Data;

namespace GoWork.Models
{
    public class DeviceToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
        public string Token { get; set; } = string.Empty;
        public string? DeviceType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
