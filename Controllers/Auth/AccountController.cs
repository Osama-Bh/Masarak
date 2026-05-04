using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.AuthDTOs;
using GoWork.DTOs.FileDTOs;
using GoWork.Models;
using GoWork.Service.AccountService;
using GoWork.Services.EmailService;
using GoWork.Services.FileService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace GoWork.Controllers.Auth
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IAccountService _accountService;
        private readonly string _frontendBaseUrl;
        private readonly IFileService _fileService;
        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IEmailService emailService, IConfiguration configuration, IAccountService accountService,IFileService fileService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _accountService = accountService;
            _frontendBaseUrl = configuration["Frontend:BaseUrl"];
            _fileService = fileService;
        }

        //this endpoint to list all emails 
        [HttpGet("ListAllEmails")]
        public async Task<IActionResult> GetInfo()
        {
            var emails = await _context.Users
          .Select(u => u.Email)
          .ToListAsync();

            return Ok(emails);
        }

        //[EnableRateLimiting("ResendPolicy")]
        [HttpPost("ResendOtp")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ResendOtp(ResendOtpDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var response = await _accountService.ResendOtpAsync(dto);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }

            return Ok(response);
        }

        //[EnableRateLimiting("ResendPolicy")]
        [HttpPost("ResendLink")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ResendResetPasswordLing(ResendOtpDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var response = await _accountService.ResendLinkAsync(dto);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpDelete("DeleteByEmail")]
        public async Task<IActionResult> DeleteByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
                return NotFound("User not found.");

            var employer = await _context.TbEmployers
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

            if (employer != null)
            {
                _context.TbEmployers.Remove(employer);
                await _context.SaveChangesAsync();
            }

            var Role = await _context.UserRoles
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

            if (Role != null)
            {
                _context.UserRoles.Remove(Role);
                await _context.SaveChangesAsync();
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("User deleted successfully.");
        }

        // This is for both company and admin because they have the same response and we will differentiate between them in the service layer
        [Authorize]
        [HttpDelete("DeleteAccount")]
        public async Task<IActionResult> DeleteAccount()
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int Id))
            {
                return Unauthorized("Unauthorized: Id not found.");
            }

            var response = await _accountService.DeleteAccountAsync(Id);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }

            Response.Cookies.Delete("access_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Domain = ".masarak.app"
            });
            return Ok(response);
        }

        //This is for both company and admin because they have the same response
        [Authorize]
        [HttpPatch("ChangePassword")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ChangePassword(ChangePasswordDTO changePasswordDto)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);

            if (claims == null || !int.TryParse(claims.Value, out int userId))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, new ConfirmationResponseDTO
                {
                    Message = "Unauthorized: Id not found."
                });
            }

            var response  = await _accountService.ChangePasswordAsync(userId, changePasswordDto);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);

            }

            return Ok(response);
        }

        [Authorize]
        [HttpPatch("UpdateProfile")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateCompanyProfile(UpdateCompanyProfileDTO updateCompanyProfileDTO)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);

            if (claims == null || !int.TryParse(claims.Value, out int userId))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, new ConfirmationResponseDTO
                {
                    Message = "Unauthorized: Id not found."
                });
            }

            var response = await _accountService.UpdateCompanyProfileAsync(userId, updateCompanyProfileDTO);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);

            }

            return Ok(response);
        }

        [Authorize]
        [HttpPatch("Admin/UpdateProfile")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateAdminProfile(UpdateAdminProfileDTO updateAdminProfileDTO)
        {
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);

            if (claims == null || !int.TryParse(claims.Value, out int userId))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, new ConfirmationResponseDTO
                {
                    Message = "Unauthorized: Id not found."
                });
            }

            var response = await _accountService.UpdateAdminProfileAsync(userId, updateAdminProfileDTO);

            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);

            }

            return Ok(response);
        }


        [Authorize(Roles = "Admin")]
        [HttpGet("AdminTest")]
        public IActionResult GetAdmin()
        {
            return Ok("Welcome Admin");
        }

        [Authorize(Roles = "Company")]
        [HttpGet("CompanyTest")]
        public IActionResult GetCompany()
        {
            return Ok("Welcome Company");
        }

        [Authorize]
        [HttpGet("Me")]
        public async Task<IActionResult> Me()
        {

            //if (!ModelState.IsValid)
            //{
            //    return BadRequest("Invalid request data.");
            //}
            // Retrieve the CandidateId from the authenticated user's claims
            var claims = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int Id))
            {
                return Unauthorized("Unauthorized: Id not found.");
            }

            var clientType = Request.Headers["ClientType"].ToString();
            if (clientType == "web")
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Id);

                if (user == null)
                    return NotFound("Not Found");

                var role = User.FindFirstValue(ClaimTypes.Role);

                if (role == "Admin")
                {
                    return Ok(new EmployerResponseDTO
                    {
                        Email = user.Email,
                        Role = role,
                        Name = "Masarak",
                        PhoneNumber = user.PhoneNumber
                    });
                }
                else if(role == "SubAdmin")
                {
                    return Ok(new EmployerResponseDTO
                    {
                        Email = user.Email,
                        Role = role,
                        Name = user.Name,
                        PhoneNumber = user.PhoneNumber,
                        Status = user.Status
                    });
                }

                var employer = await _context.TbEmployers
                .Include(e => e.EmployerStatus)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.UserId == user.Id);

                if (employer == null)
                    return NotFound("Employer profile not found.");

                var logoUrlResponse =  _fileService
                    .DownloadUrlAsync(employer.LogoUrl);

                return Ok(new EmployerResponseDTO
                {
                    Email = user.Email,
                    Role = role,
                    Name = employer.ComapnyName,
                    PhoneNumber = user.PhoneNumber,
                    SasUrl = logoUrlResponse.SasUrl,
                    ExpiresAt = logoUrlResponse.ExpiresAt,
                    Industry = employer.Industry,
                    Status = employer.EmployerStatus.Name
                });
            }
            else
            {
                var response = await _accountService.GetCandidateProfileAsync(Id);
                if (response.StatusCode != 200)
                {
                    return StatusCode((int)response.StatusCode, response);
                }
                return Ok(response);
            }




            //var user = await _userManager.GetUserAsync(User);
            //if (user == null)
            //    return Unauthorized();

            //var roles = await _userManager.GetRolesAsync(user);
            //var role = roles.FirstOrDefault();
            //if (role == "Admin")
            //{
            //    var adminResponse = new EmployerResponseDTO
            //    {
            //        Email = user.Email,
            //        Role = role,
            //        CompanyName = "Masarak",
            //        PhoneNumber = user.PhoneNumber
            //    };

            //    return Ok(adminResponse);
            //}
            //else
            //{
            //    var employer = await _context.TbEmployers
            //    .FirstOrDefaultAsync(e => e.UserId == user.Id);

            //    var LogoUrlRespons = _fileService.DownloadUrlAsync(employer.LogoUrl);

            //    var response = new EmployerResponseDTO
            //    {
            //        Email = user.Email,
            //        Role = role,
            //        CompanyName = employer?.ComapnyName,
            //        PhoneNumber = user.PhoneNumber,
            //        SasUrl = LogoUrlRespons.SasUrl,
            //        ExpiresAt = LogoUrlRespons.ExpiresAt,
            //        Industry = employer.Industry
            //    };

            //    return Ok(response);
            //}

                
        }

        [HttpPost("Candidate/Register")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> CandidateRegister([FromForm]CandidateRegistrationDTO candidateRegistrationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid registration data.");
            }

            var response = await _accountService.CandidateRegisterAsync(candidateRegistrationDTO);
            if (response.StatusCode != 201)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpPost("Register")]
        public async Task<ActionResult<ApiResponse<EmployerResponseDTO>>> EmployerRegister([FromForm] EmpolyerRegistrationDTO employerRegistrationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid registration data.");
            }
            var response = await _accountService.RegisterCompany(employerRegistrationDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Admin/Register")]
        public async Task<ActionResult<ApiResponse<EmployerResponseDTO>>> AdminRegister(AdminRegistrationDTO adminRegistrationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid registration data!.");
            }
            var response = await _accountService.RegisterAdmin(adminRegistrationDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        //[HttpPost("Admin/VerifyEmail")]
        //public async Task<ActionResult<ApiResponse<EmployerResponseDTO>>> VerifyAdmnEmail(EmailConfirmationDTO confirmationDTO)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest("Invalid Confirmation data.");
        //    }

        //    var response = await _accountService.VerifyAdminEmail(confirmationDTO);
        //    if (response.StatusCode != 200)
        //    {
        //        return StatusCode((int)response.StatusCode, response);
        //    }

        //    // ✅ Generate JWT
        //    var user = await _userManager.FindByEmailAsync(confirmationDTO.Email);
        //    var token = _accountService.GenerateJwtToken(user);

        //    // ✅ Inject cookie
        //    Response.Cookies.Append("access_token", token, new CookieOptions
        //    {
        //        HttpOnly = true,
        //        Secure = true,
        //        SameSite = SameSiteMode.None,
        //        Expires = DateTime.UtcNow.AddDays(7),
        //        Path = "/",
        //        Domain = ".masarak.app"
        //    });

        //    return Ok(response);
        //}


        [HttpPost("Candidate/VerifyEmail")]
        public async Task<ActionResult<ApiResponse<CandidateResponseDTO2>>> VerifyEmail(EmailConfirmationDTO confirmationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Confirmation data.");
            }

            var response = await _accountService.VerifyEmail(confirmationDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpPost("Candidate/Login")]
        public async Task<ActionResult<ApiResponse<LoginResponseDTO>>> Login(LoginDTO loginDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Login data.");
            }

            var response = await _accountService.Login(loginDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }


        //Added
        //This login for both company and admin because they have the same response and we will differentiate between them in the service layer
        [HttpPost("Login")]
        public async Task<ActionResult<ApiResponse<EmployerResponseDTO>>> LoginCompany(LoginDTO loginDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Login data.");
            }
            var clientType = Request.Headers["ClientType"].ToString();
            if (clientType == "web")
            {
                var response = await _accountService.LoginAdminAndCompany(loginDTO);
                if (response.StatusCode != 200)
                {
                    return StatusCode((int)response.StatusCode, response);
                }

                //var token = response.Data.Token;
                var user = await _userManager.FindByEmailAsync(loginDTO.Email);
                var token = _accountService.GenerateJwtToken(user);

                Response.Cookies.Append("access_token", token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Path = "/",
                    Domain = ".masarak.app"
                });

                return Ok(response);
            }
            else
            {
                var response = await _accountService.Login(loginDTO);
                if (response.StatusCode != 200)
                {
                    return StatusCode((int)response.StatusCode, response);
                }
                return Ok(response);


            }
        }

        //Added

        [HttpPost("VerifyEmail")]
        public async Task<ActionResult<ApiResponse<EmployerResponseDTO>>> VerifyCompanyEmail(EmailConfirmationDTO confirmationDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Confirmation data.");
            }

            var response = await _accountService.VerifyCompanyEmail(confirmationDTO);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }

            // ✅ Generate JWT
            var user = await _userManager.FindByEmailAsync(confirmationDTO.Email);
            var token = _accountService.GenerateJwtToken(user);

            // ✅ Inject cookie
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7),
                Path = "/",
                Domain = ".masarak.app"
            });

            return Ok(response);
        }

        
        // Added
        [Authorize]
        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Domain = ".masarak.app"
            });

            return Ok("Logout Successfully");
        }

        [Authorize]
        [HttpPatch("Candidate/UpdateProfile")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> UpdateCandidateProfile([FromForm]UpdateProfileDTO profileDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }
            // Retrieve the CandidateId from the authenticated user's claims
            var candidateIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (candidateIdClaim == null || !int.TryParse(candidateIdClaim.Value, out int candidateId))
            {
                return Unauthorized("Unauthorized: CandidateId not found.");
            }
            var response = await _accountService.UpdateCandidateProfileAsync(candidateId, profileDTO);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode,  response);
            }
            return Ok(response);
        }

        [Authorize]
        [HttpGet("candidate/profile/me")]
        public async Task<ActionResult<ApiResponse<CandidateResponseDTO>>> GetCandidateProfile()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid request data.");
            }
            // Retrieve the CandidateId from the authenticated user's claims
            var candidateIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (candidateIdClaim == null || !int.TryParse(candidateIdClaim.Value, out int candidateId))
            {
                return Unauthorized("Unauthorized: CandidateId not found.");
            }
            var response = await _accountService.GetCandidateProfileAsync(candidateId);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [Authorize]
        [HttpPost("candidate/uploadfile")] // for candidate // upload and update 
        public async Task<ActionResult<ConfirmationResponseDTO>> UploadFile(IFormFile file)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid data.");
            }

            var UserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (UserIdClaim == null || !int.TryParse(UserIdClaim.Value, out int userId))
            {
                return Unauthorized("Unauthorized: CandidateId not found.");
            }

            var reslut = await _accountService.UploadFile(file, userId);
            if (reslut.StatusCode != 200)
            {
                return StatusCode(reslut.StatusCode, reslut);
            }
            return Ok(reslut);
        }

        [Authorize]
        [HttpGet("candidate/me/profilepicture")]
        public async Task<ActionResult<ApiResponse<FileDownloadDto>>> GetProfileImage()
        {
            var UserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (UserIdClaim == null || !int.TryParse(UserIdClaim.Value, out int userId))
            {
                return Unauthorized("Unauthorized: CandidateId not found.");
            }

            var response = await _accountService.DownloadCandidateProfilePicOrResume(userId, Enums.FileCategoryEnum.ProfilePIcture);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [Authorize]
        [HttpGet("candidate/me/resume")]
        public async Task<ActionResult<ApiResponse<FileDownloadDto>>> GetResume()
        {
            var UserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (UserIdClaim == null || !int.TryParse(UserIdClaim.Value, out int userId))
            {
                return Unauthorized("Unauthorized: CandidateId not found.");
            }

            var response = await _accountService.DownloadCandidateProfilePicOrResume(userId, Enums.FileCategoryEnum.Resume);
            if (response.StatusCode != 200)
            {
                return StatusCode((int)response.StatusCode, response);
            }
            return Ok(response);
        }

        [HttpPost("ForgetPassword")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ForgotPassword(ForgetPasswordDTO forgetpasswordDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Email");
            }
            var response = await _accountService.ForgetPassword(forgetpasswordDTO);
            
            if(response.StatusCode !=200)
            {
                return  StatusCode((int)response.StatusCode, response);
            }

            return Ok(response);
        }

        [HttpPost("ResetPassword")]
        public async Task<ActionResult<ApiResponse<ConfirmationResponseDTO>>> ResetPassword(ResetPasswordDTO resetpasswordDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid Email");
            }
            var response = await _accountService.ResetPassword(resetpasswordDTO);

            if(response.StatusCode !=200)
            {
                return StatusCode((int)response.StatusCode, response);
            }

            return Ok(response);
        }
    }
}
