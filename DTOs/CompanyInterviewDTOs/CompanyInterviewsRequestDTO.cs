namespace GoWork.DTOs.CompanyInterviewDTOs
{
    public class CompanyInterviewsRequestDTO
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public int? InterviewStatusId { get; set; }
        public int? JobId { get; set; }

        public DateTime? CurrentDate { get; set; }
    }
}
