using System.ComponentModel.DataAnnotations;

namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class ScheduleInterviewRequestDTO
    {
        [Required(ErrorMessage = "Interview date is required.")]
        public DateTime InterviewDate { get; set; }

        [StringLength(200, ErrorMessage = "Notes cannot exceed 200 characters.")]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Interview type is required.")]
        public int InterviewTypeId { get; set; }

        // Online fields
        [StringLength(500, ErrorMessage = "Meeting link cannot exceed 500 characters.")]
        public string? MeetingLink { get; set; }

        // InPerson fields
        public int? CountryId { get; set; }
        public int? GovernateId { get; set; }
        public string? AddressLine { get; set; }
    }
}
