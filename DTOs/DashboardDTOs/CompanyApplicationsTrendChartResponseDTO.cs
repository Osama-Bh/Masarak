namespace GoWork.DTOs.DashboardDTOs
{
    public class CompanyApplicationsTrendChartResponseDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Period { get; set; } = "weekly";
        public string XKey { get; set; } = "label";
        public List<ChartSeriesDTO> Series { get; set; } = new();
        public List<CompanyApplicationsTrendChartPointDTO> Items { get; set; } = new();
    }
}
