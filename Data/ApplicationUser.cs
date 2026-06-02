using GoWork.Models;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;

namespace GoWork.Data
{
    public class ApplicationUser : IdentityUser<int>
    {
        [JsonIgnore]
        public Seeker? Seeker { get; set; }
        [JsonIgnore]
        public Employer? Employer { get; set; }
        [JsonIgnore]
        public ICollection<Feedback>? Feedbacks { get; set; }

        [JsonIgnore]
        public ICollection<UserNotification>? UserNotifications { get; set; }

        [JsonIgnore]
        public ICollection<DeviceToken>? DeviceTokens { get; set; }

        public string? Name { get; set; }

        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
