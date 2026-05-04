namespace GoWork.DTOs.InterviewDTOs
{
    public class InterviewResponseDTO
    {
        public List<InterviewDTO> Interviews { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
    }
}
