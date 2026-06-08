using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoWork.Models
{
    public class Interview
    {
        public int Id { get; set; }
        public int ApplicationId { get; set; }

        [ForeignKey("ApplicationId")]
        public Application Application { get; set; } = null!;
        public DateTime InterviewDate { get; set; }
        public int InterviewTypeId { get; set; }
        [ForeignKey("InterviewTypeId")]
        public InterviewType InterviewType { get; set; } = null!;
        public int? AddressId { get; set; }
        public Address Address { get; set; } = null!;
        [StringLength(200, ErrorMessage = "Notes cannot exceed 100 characters.")]
        public string? Notes { get; set; }
        [StringLength(500, ErrorMessage = "Meeting link cannot exceed 500 characters.")]
        public string? MeetingLink { get; set; }
        public DateTime? RespondedAt { get; set; }
        public int InterviewStatusId { get; set; }
        public InterviewStatus InterviewStatus { get; set; }
        public string? ExpirationHangfireJobId { get; set; }

    }
}
