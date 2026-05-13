namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class CompanyApplicationFiltersDTO
    {
        public List<LookUpDTO> Statuses { get; set; } = new();
        public List<LookUpDTO> Jobs { get; set; } = new();
    }
}
