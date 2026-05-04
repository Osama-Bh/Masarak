namespace GoWork.DTOs.InterviewDTOs
{
    public class InterviewDTO
    {
        public int Id { get; set; }
        public string JobTitle { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public DateTime InterviewDate { get; set; }
        public string InterviewType { get; set; } = null!;
        public string? Location { get; set; }
        public string? MeetingLink { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = null!;
    }
}
