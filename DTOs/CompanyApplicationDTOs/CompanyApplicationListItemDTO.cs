namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class CompanyApplicationListItemDTO
    {
        public int ApplicationId { get; set; }
        public string? ProfilePhoto { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public DateTime ApplicationDate { get; set; }
        public int? MatchingPercentage { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public string? CvDownloadUrl { get; set; }

        // Dynamic action flags
        public bool CanReject { get; set; }
        public bool CanSchedule { get; set; }
        public bool CanHire { get; set; }
    }
}
