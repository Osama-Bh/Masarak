namespace GoWork.DTOs.CompanyApplicationDTOs
{
    public class CompanyEmploymentRecordsStatisticsDTO
    {
        public int TotalRecords { get; set; }
        public int WithdrawnRecords { get; set; }
        public int MissingInterviewRecords { get; set; }
        public int HiredRecords { get; set; }
        public int RejectedRecords { get; set; }
    }
}
