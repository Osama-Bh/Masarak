namespace GoWork.DTOs.DashboardDTOs
{
    public class CompanyApplicationDTO
    {
        public int ApplicationId { get; set; }
        public string CandidateName { get; set; } = null!;
        public string CandidateEmail { get; set; } = null!;
        public string JobTitle { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string CandidateDescription { get; set; } = null!;
        public string? ResumeUrl { get; set; }
        public int StatusId { get; set; }
        public string StatusName { get; set; } = null!;
        public bool CanAction { get; set; }
    }
}
