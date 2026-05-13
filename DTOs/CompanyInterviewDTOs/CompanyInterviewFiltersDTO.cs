using GoWork.DTOs;

namespace GoWork.DTOs.CompanyInterviewDTOs
{
    public class CompanyInterviewFiltersDTO
    {
        public List<LookUpDTO> Statuses { get; set; } = new();
        public List<LookUpDTO> Jobs { get; set; } = new();
    }
}
