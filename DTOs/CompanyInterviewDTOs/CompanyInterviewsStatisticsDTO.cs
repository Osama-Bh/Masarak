namespace GoWork.DTOs.CompanyInterviewDTOs
{
    public class CompanyInterviewsStatisticsDTO
    {
        public int TotalInterviews { get; set; }
        public int ScheduledInterviews { get; set; }
        public int ConfirmedInterviews { get; set; }
        public int CompletedInterviews { get; set; }
        public int TodayInterviews { get; set; }
    }
}
