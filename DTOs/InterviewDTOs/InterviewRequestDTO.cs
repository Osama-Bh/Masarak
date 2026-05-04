using System.ComponentModel.DataAnnotations;

namespace GoWork.DTOs.InterviewDTOs
{
    public class InterviewRequestDTO
    {
        public int? InterviewStatusId { get; set; }

        // "asc" or "desc"
        public string? SortOrder { get; set; } = "desc";

        [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
        public int Page { get; set; } = 1;

        [Range(1, 50, ErrorMessage = "PageSize must be between 1 and 50.")]
        public int PageSize { get; set; } = 30;

        [System.Text.Json.Serialization.JsonIgnore]
        public int? UserId { get; set; }
    }
}
