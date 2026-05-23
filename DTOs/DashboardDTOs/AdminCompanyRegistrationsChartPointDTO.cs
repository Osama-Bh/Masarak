namespace GoWork.DTOs.DashboardDTOs
{
    public class AdminCompanyRegistrationsChartPointDTO
    {
        public string Label { get; set; } = string.Empty;
        public int CompaniesRegistered { get; set; }
        public int PendingVerifications { get; set; }
    }
}
