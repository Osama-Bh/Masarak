namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class JobApplicantDTO
    {
        public int ApplicationId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int? MatchingPercentage { get; set; }
        public DateTime ApplicationDate { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public string? CvDownloadUrl { get; set; }
    }
}
