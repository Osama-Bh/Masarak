using ECommerceApp.DTOs;
using GoWork.DTOs.AuthDTOs;
using GoWork.DTOs.DashboardDTOs;

namespace GoWork.Services.AdminService
{
    public interface IAdminService
    {
        Task<ApiResponse<AdminDashboardStatisticsDTO>> GetAdminDashboardStatisticsAsync();
        Task<ApiResponse<ChartPieResponseDTO>> GetAdminCompanyStatusDistributionChartAsync();
        Task<ApiResponse<AdminCompanyRegistrationsChartResponseDTO>> GetAdminCompanyRegistrationsChartAsync();
        Task<ApiResponse<CompanyStatisticsDTO>> GetCompanyStatisticsAsync();

        Task<ApiResponse<PaginatedResult<CompanyListItemDTO>>> GetCompaniesAsync(
            int page, int pageSize, string? search, string? status, string? sortBy, string? sortOrder);

        Task<ApiResponse<CompanyDetailDTO>> GetCompanyByIdAsync(int id);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateCompanyStatusAsync(int id, UpdateCompanyStatusDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> DeleteCompanyAsync(int id);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateCompanyAsync(int id, AdminUpdateCompanyDTO dto);

        Task<ApiResponse<BulkActionResultDTO>> BulkActionAsync(BulkActionDTO dto);

        // Sub-admin management
        Task<ApiResponse<SubAdminStatisticsDTO>> GetSubAdminStatisticsAsync();

        Task<ApiResponse<PaginatedResult<SubAdminListItemDTO>>> GetSubAdminsAsync(
            int page, int pageSize, string? search, string? status);

        Task<ApiResponse<SubAdminDetailDTO>> GetSubAdminByIdAsync(int id);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateSubAdminStatusAsync(int id, UpdateSubAdminStatusDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> DeleteSubAdminAsync(int id);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateSubAdminAsync(int id, UpdateSubAdminDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> CreateSubAdminAsync(AdminRegistrationDTO dto);
    }
}
