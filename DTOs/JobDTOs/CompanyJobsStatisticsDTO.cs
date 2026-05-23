namespace GoWork.DTOs.JobDTOs
{
    public class CompanyJobsStatisticsDTO
    {
        public int TotalJobs { get; set; }
        public int ActiveJobs { get; set; }
        public int ExpiredJobs { get; set; }
        public int FullTimeJobs { get; set; }
        public int PartTimeJobs { get; set; }
    }
}
