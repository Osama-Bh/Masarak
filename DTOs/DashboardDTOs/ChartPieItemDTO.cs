namespace GoWork.DTOs.DashboardDTOs
{
    public class ChartPieItemDTO
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Fill { get; set; } = string.Empty;
    }
}
