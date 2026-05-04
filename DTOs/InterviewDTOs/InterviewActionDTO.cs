using System.ComponentModel.DataAnnotations;

namespace GoWork.DTOs.InterviewDTOs
{
    public class InterviewActionDTO
    {
        [Required(ErrorMessage = "Action is required.")]
        public string Action { get; set; } = null!; // Accept" or "Cancel"
    }
}
