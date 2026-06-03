using ECommerceApp.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.JobDTOs;

namespace GoWork.Services.JobService
{
    public interface IJobService
    {
        // Job CRUD
        Task<ApiResponse<CompanyJobsStatisticsDTO>> GetJobsStatisticsAsync(int employerId);

        Task<ApiResponse<PaginatedResult<JobListItemDTO>>> GetJobsAsync(
            int employerId, int page, int pageSize, string? search, string? status, int? jobTypeId);

        Task<ApiResponse<JobDetailDTO>> GetJobByIdAsync(int employerId, int id);

        Task<ApiResponse<ConfirmationResponseDTO>> CreateJobAsync(int employerId, CreateJobDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateJobAsync(int employerId, int id, UpdateJobDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateJobStatusAsync(int employerId, int id, UpdateJobStatusDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> DeleteJobAsync(int employerId, int id);

        Task<ApiResponse<JobDescriptionEnhancementResultDTO>> EnhanceJobDescriptionAsync(EnhanceJobDescriptionDTO dto);

        Task<ApiResponse<JobRecommendationResultDto>> GetJobRecommendationsAsync(int seekerId);

        Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobSearchRequestDto request);

        Task<ApiResponse<JobDetailsDto>> GetJobDetailsAsync(int jobId, int? seekerId);

        Task<ApiResponse<ApplicationResultDto>> ApplyToJobAsync(int jobId, int seekerId);

        // Lookups
        Task<ApiResponse<List<LookupDTO>>> GetCategoriesAsync(string? search);
        Task<ApiResponse<List<LookupDTO>>> GetJobTypesAsync();
        Task<ApiResponse<List<LookupDTO>>> GetLocationTypesAsync();
        Task<ApiResponse<List<CurrencyLookupDTO>>> GetCurrenciesAsync(string? search);
        Task<ApiResponse<List<CountryLookupDTO>>> GetCountriesAsync(string? search);
        Task<ApiResponse<List<LookupDTO>>> GetGovernatesAsync(int countryId, string? search);
        Task<ApiResponse<List<SkillDTO>>> GetSkillsAsync(string? search);
    }

    public class LookupDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CurrencyLookupDTO
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CountryLookupDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
