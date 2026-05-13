namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class CompanyApplicationsRequestDTO
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public int? ApplicationStatusId { get; set; }
        public int? JobId { get; set; }
    }
}
