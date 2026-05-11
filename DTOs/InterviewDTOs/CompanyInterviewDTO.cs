namespace GoWork.DTOs.InterviewDTOs
{
    public class CompanyInterviewDTO
    {
        //public int InterviewId { get; set; }
        //public int ApplicationId { get; set; }
        //public string CandidateName { get; set; } = null!;
        //public string CandidateEmail { get; set; } = null!;
        //public string JobTitle { get; set; } = null!;
        //public DateTime InterviewDate { get; set; }
        //public string InterviewTypeName { get; set; } = null!;
        //public string Location { get; set; } = null!;
        //public int StatusId { get; set; }
        //public string StatusName { get; set; } = null!;
        //public string? Notes { get; set; }

        public int InterviewId { get; set; }
        public int ApplicationId { get; set; }
        public string CandidateName { get; set; } = null!;
        public string CandidateEmail { get; set; } = null!;
        public string JobTitle { get; set; } = null!;
        public DateTime InterviewDate { get; set; }
        public string InterviewTypeName { get; set; } = null!;
        public string Location { get; set; } = null!;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = null!;
        public string? Notes { get; set; }

        // Action Flags
        public bool CanReschedule { get; set; }
        public bool CanCancel { get; set; }
        public bool CanComplete { get; set; }
        public bool CanMarkAsMissing { get; set; }
    }
}
