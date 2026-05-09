namespace GoWork.DTOs.InterviewDTOs
{
    public class ScheduleInterviewDTO
    {
        [Required]
        public int ApplicationId { get; set; }

        [Required]
        public DateTime InterviewDate { get; set; }

        [Required]
        public int InterviewTypeId { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? MeetingLink { get; set; }

        [Required]
        [MaxLength(200)]
        public string AddressLine1 { get; set; } = null!;

        [Required]
        public int CountryId { get; set; }

        [Required]
        public int GovernateId { get; set; }
    }
}
