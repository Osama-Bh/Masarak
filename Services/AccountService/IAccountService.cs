using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.AuthDTOs;
using GoWork.DTOs.FileDTOs;
using GoWork.Enums;

namespace GoWork.Service.AccountService
{
    public interface IAccountService
    {
        // Candidate
        Task<ApiResponse<ConfirmationResponseDTO>> CandidateRegisterAsync(
            CandidateRegistrationDTO registrationDTO
        );

        //Task<ApiResponse<ConfirmationResponseDTO>> UploadFile(UploadFileRequestDTO fileRequestDTO, FileCategoryEnum fileCategory);


        Task<ApiResponse<CandidateResponseDTO2>> VerifyEmail(
            EmailConfirmationDTO confirmationDTO
        );

        Task<ApiResponse<LoginResponseDTO>> Login(
            LoginDTO loginDTO
        );

        // Employer
        Task<ApiResponse<ConfirmationResponseDTO>> RegisterCompany(
            EmpolyerRegistrationDTO registrationDTO
        );

        Task<ApiResponse<EmployerResponseDTO>> VerifyCompanyEmail(
            EmailConfirmationDTO confirmationDTO
        );

        Task<ApiResponse<EmployerResponseDTO>> LoginCompany(
            LoginDTO loginDTO
        );

        Task<ApiResponse<ConfirmationResponseDTO>> ForgetPassword(
            ForgetPasswordDTO forgetpasswordDTO, string clientType
        );

        Task<ApiResponse<ConfirmationResponseDTO>> ResetPassword(
            ResetPasswordDTO resetpasswordDTO, string clientType
        );

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateCandidateProfileAsync(int candidateId, UpdateProfileDTO dto);
        Task<ApiResponse<ConfirmationResponseDTO>> AddCandidateAddressAsync(int userId, AddAddressRequestDTO dto);
        Task<ApiResponse<CandidateResponseDTO>> GetCandidateProfileAsync(int userId);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateFile(UpdateFileRequestDTO requestDTO, FileCategoryEnum fileCategory);

        Task<ApiResponse<FileDownloadDto>> DownloadCandidateProfilePicOrResume(int userId, FileCategoryEnum fileCategory);
        Task<ApiResponse<ConfirmationResponseDTO>> UploadFile(IFormFile file, int userId);
        Task<ApiResponse<ConfirmationResponseDTO>> DeleteAccountAsync(int userId);
        Task<ApiResponse<ConfirmationResponseDTO>> ChangePasswordAsync(int userId, ChangePasswordDTO changePasswordDto);
        Task<ApiResponse<ConfirmationResponseDTO>> UpdateCompanyProfileAsync(int userId, UpdateCompanyProfileDTO dto);

        Task<ApiResponse<ConfirmationResponseDTO>> ResendOtpAsync(ResendOtpDTO resendDto);

        Task<ApiResponse<ConfirmationResponseDTO>> ResendLinkAsync(ResendOtpDTO resendDto);
        Task<ApiResponse<ConfirmationResponseDTO>> RegisterAdmin(AdminRegistrationDTO adminRegistrationDTO);

        //Task<ApiResponse<EmployerResponseDTO>> VerifyAdminEmail(EmailConfirmationDTO confirmationDTO);

        Task<ApiResponse<EmployerResponseDTO>> LoginAdminAndCompany(LoginDTO loginDTO);

        Task<ApiResponse<ConfirmationResponseDTO>> UpdateAdminProfileAsync(int userId, UpdateAdminProfileDTO dto);

        /// <summary>
        /// Deletes a Seeker by their Seeker.Id and cascades to all related entities,
        /// including the linked ApplicationUser and their notifications, device tokens, and feedbacks.
        /// </summary>
        Task<ApiResponse<ConfirmationResponseDTO>> DeleteCandidateAccountAsync(int userId);

        // Token
        string GenerateJwtToken(ApplicationUser user);
    }

}
