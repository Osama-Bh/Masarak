namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class CompanyApplicationsStatisticsDTO
    {
        public int TotalApplications { get; set; }
        public int PendingReviewApplications { get; set; }
        public int ShortlistedApplications { get; set; }
        public int InterviewedApplications { get; set; }
        public int ApplicationsThisWeek { get; set; }
    }
}
