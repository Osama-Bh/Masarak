namespace GoWork.DTOs.CompanyInterviewDTOs
{
    public class CompanyInterviewListItemDTO
    {
        public int InterviewId { get; set; }
        public int ApplicationId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public DateTime InterviewDate { get; set; }
        public string InterviewType { get; set; } = string.Empty;
        public string InterviewStatus { get; set; } = string.Empty;
        public string? Location { get; set; }

        // Dynamic action flags
        public bool CanCancel { get; set; }
        public bool CanReschedule { get; set; }
        public bool CanComplete { get; set; }
        public bool CanMarkMissing { get; set; }
    }
}
