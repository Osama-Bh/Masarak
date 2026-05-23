namespace GoWork.DTOs.DashboardDTOs
{
    public class ChartPieResponseDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ChartPieItemDTO> Items { get; set; } = new();
    }
}
