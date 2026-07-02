using ECommerceApp.DTOs;
using GoWork.Authorization.Operations;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Infrastructure.Hangfire;
using GoWork.Models;
using GoWork.Services.CurrentUserService;
using GoWork.Services.EmailService;
using GoWork.Services.FileService;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Drawing;

namespace GoWork.Services.ApplicationService
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;


        public ApplicationService(
            ApplicationDbContext context,
            IFileService fileService,
            ICurrentUserService currentUserService,
            IAuthorizationService authorizationService,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService)
        {
            _context = context;
            _fileService = fileService;
            _currentUserService = currentUserService;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
            _emailService = emailService;
        }

        private TimeZoneInfo GetTimeZoneFromHeader()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var timeZoneHeader = httpContext?.Request.Headers["TimeZone"].ToString()
                                 ?? httpContext?.Request.Headers["Time-Zone"].ToString();

            if (string.IsNullOrWhiteSpace(timeZoneHeader))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneHeader);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        public async Task<ApiResponse<List<LookUpDTO>>> GetApplicationStatuses()
        {
            var items = await _context.TbApplicationStatuses
                .Where(a => a.IsActive)
                .OrderBy(a => a.SortOrder)
                .Select(a => new LookUpDTO
                {
                    Id = a.Id,
                    Name = a.Name
                })
                .ToListAsync();

            return new ApiResponse<List<LookUpDTO>>(200, items);
        }

        public async Task<ApiResponse<ApplicationsResponseDTO>> GetCandidateApplications(ApplicationsRequestDTO requestDTO)
        {
            if (!requestDTO.UserId.HasValue)
                return new ApiResponse<ApplicationsResponseDTO>(401, "unauthorized user");

            // Single async query for the seeker
            var seeker = await _context.TbSeekers
                .FirstOrDefaultAsync(s => s.UserId == requestDTO.UserId.Value);

            if (seeker == null)
                return new ApiResponse<ApplicationsResponseDTO>(404, "seeker not found");


            // Build base query filtered by seeker
            var baseQuery = _context.TbApplications.Where(a => a.SeekerId == seeker.Id);


            // Apply optional status filter
            if (requestDTO.ApplicationStatusId.HasValue)
            {
                baseQuery = baseQuery.Where(a => a.ApplicationStatusId == requestDTO.ApplicationStatusId.Value);
            }

            // Apply sorting (default: newest first)
            baseQuery = string.Equals(requestDTO.SortOrder, "desc", StringComparison.OrdinalIgnoreCase)
                ? baseQuery.OrderByDescending(a => a.ApplicationDate)
                : baseQuery.OrderBy(a => a.ApplicationDate);

            // Count BEFORE pagination
            var totalCount = await baseQuery.CountAsync();

            // Apply pagination
            var pagedApplications = await baseQuery
                .Skip((requestDTO.Page - 1) * requestDTO.PageSize)
                .Take(requestDTO.PageSize)
                .Select(a => new ApplicationDTO
                {
                    ApplicationId = a.Id,
                    JobId = a.JobId,
                    JobTitle = a.Job.Title,
                    CompanyName = a.Job.Employer.ComapnyName,
                    CompanyLogo = a.Job.Employer.LogoUrl,
                    ApplicationStatus = a.ApplicationStatus.Name,
                    AppliedDate = a.ApplicationDate,
                    CanWithdraw = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview
                })
                .ToListAsync();

            // Resolve company logo URLs via file service
            foreach (var application in pagedApplications)
            {
                if (!string.IsNullOrEmpty(application.CompanyLogo))
                    application.CompanyLogo = _fileService.DownloadUrlAsync(application.CompanyLogo)?.SasUrl;
                else
                    application.CompanyLogo = null;
            }

            var totalPages = (int)Math.Ceiling((double)totalCount / requestDTO.PageSize);

            return new ApiResponse<ApplicationsResponseDTO>(200, new ApplicationsResponseDTO
            {
                Applications = pagedApplications,
                PageNumber = requestDTO.Page,
                PageSize = requestDTO.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = requestDTO.Page < totalPages
            });
        }


        public async Task<ApiResponse<ConfirmationResponseDTO>> WithdrawApplicationAsync(int applicationId, int userId)
        {
            var seeker = await _context.TbSeekers
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (seeker == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "seeker not found");


            var application = await _context.TbApplications.Include(a => a.Seeker)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            var authorizationResult = await _authorizationService.AuthorizeAsync(_currentUserService.User, application,
                ApplicationOperations.Withdraw);

            if (!authorizationResult.Succeeded)
                return new ApiResponse<ConfirmationResponseDTO>(403, "You are not authorized to withdraw this application.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Only applications under pending review can be withdrawn.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Withdrawn;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Application withdrawn successfully." });
        }

        // ======================== COMPANY DASHBOARD METHODS ========================

        private IQueryable<Application> BuildCompanyApplicationsQuery(int employerId, CompanyApplicationsRequestDTO request)
        {
            var query = _context.TbApplications
                .Where(a => a.Job.EmployerId == employerId);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                query = query.Where(a =>
                    a.Job.Title.ToLower().Contains(search) ||
                    (a.Seeker.FirsName + " " + a.Seeker.MiddleName + " " + a.Seeker.LastName).ToLower().Contains(search)
                );
            }

            if (request.ApplicationStatusId.HasValue)
            {
                query = query.Where(a => a.ApplicationStatusId == request.ApplicationStatusId.Value);
            }

            if (request.JobId.HasValue)
            {
                query = query.Where(a => a.JobId == request.JobId.Value);
            }

            return query;
        }

        private IQueryable<Application> FilterCompanyApplicationsByActivity(IQueryable<Application> query, bool activeOnly)
        {
            var today = DateTime.UtcNow.Date;

            var historicalQuery = query.Where(a =>
                (
                    a.ApplicationStatusId == (int)ApplicationStatusEnum.Withdrawn &&
                    a.ApplicationDate.Date < today
                )
                ||
                (
                    a.ApplicationStatusId == (int)ApplicationStatusEnum.MissingInterview &&
                    a.Interviews
                        .OrderByDescending(i => i.InterviewDate)
                        .Select(i => (DateTime?)i.InterviewDate.Date)
                        .FirstOrDefault() < today
                )
                ||
                (
                    (a.ApplicationStatusId == (int)ApplicationStatusEnum.Rejected ||
                     a.ApplicationStatusId == (int)ApplicationStatusEnum.Hired) &&
                    (
                        (
                            a.Interviews.Any() &&
                            a.Interviews
                                .OrderByDescending(i => i.InterviewDate)
                                .Select(i => (DateTime?)i.InterviewDate.Date)
                                .FirstOrDefault() < today
                        )
                        ||
                        (
                            !a.Interviews.Any() &&
                            a.ApplicationDate.Date < today
                        )
                    )
                )
            );

            return activeOnly
                ? query.Where(a => !historicalQuery.Select(h => h.Id).Contains(a.Id))
                : historicalQuery;
        }

        private static IQueryable<Application> OrderCompanyApplications(IQueryable<Application> query, bool activeOnly)
        {
            return activeOnly
                ? query.OrderByDescending(a => a.ApplicationDate).ThenByDescending(a => a.Id)
                : query.OrderByDescending(a => a.ApplicationDate).ThenByDescending(a => a.Id);
        }

        private async Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyApplicationsByActivityAsync(
            int employerId,
            CompanyApplicationsRequestDTO request,
            bool activeOnly)
        {
            var baseQuery = BuildCompanyApplicationsQuery(employerId, request);
            baseQuery = FilterCompanyApplicationsByActivity(baseQuery, activeOnly);
            baseQuery = OrderCompanyApplications(baseQuery, activeOnly);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => new CompanyApplicationListItemDTO
                {
                    ApplicationId = a.Id,
                    ProfilePhoto = a.Seeker.ProfilePhoto,
                    FullName = a.Seeker.FirsName + " " + a.Seeker.MiddleName + " " + a.Seeker.LastName,
                    Email = a.Seeker.ApplicationUser.Email ?? string.Empty,
                    JobTitle = a.Job.Title,
                    ApplicationDate = a.ApplicationDate,
                    MatchingPercentage = a.MatchingPercentage,
                    ApplicationStatus = a.ApplicationStatus.Name,
                    CvDownloadUrl = a.Seeker.ResumeUrl,
                    CanReject = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview
                             || a.ApplicationStatusId == (int)ApplicationStatusEnum.Shortlisted
                             || a.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed,
                    CanSchedule = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview,
                    CanHire = a.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed
                })
                .ToListAsync();

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.ProfilePhoto))
                    item.ProfilePhoto = _fileService.DownloadUrlAsync(item.ProfilePhoto)?.SasUrl;

                if (!string.IsNullOrEmpty(item.CvDownloadUrl))
                    item.CvDownloadUrl = _fileService.DownloadUrlAsync(item.CvDownloadUrl)?.SasUrl;
            }

            return new ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>(200,
                new PaginatedResult<CompanyApplicationListItemDTO>
                {
                    Items = items,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = totalCount
                });
        }

        public async Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyApplicationsAsync(
            int employerId, CompanyApplicationsRequestDTO request)
        {
            return await GetCompanyApplicationsByActivityAsync(employerId, request, activeOnly: true);
        }

        public async Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyEmploymentRecordsAsync(
            int employerId, CompanyApplicationsRequestDTO request)
        {
            return await GetCompanyApplicationsByActivityAsync(employerId, request, activeOnly: false);
        }

        public async Task<ApiResponse<CompanyApplicationFiltersDTO>> GetCompanyApplicationFiltersAsync(int employerId)
        {
            var statuses = await _context.TbApplicationStatuses
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new LookUpDTO { Id = s.Id, Name = s.Name })
                .ToListAsync();

            // Get employer's jobs for the job filter dropdown
            var jobs = await _context.TbJobs
                .Where(j => j.EmployerId == employerId)
                .OrderBy(j => j.Title)
                .Select(j => new LookUpDTO { Id = j.Id, Name = j.Title })
                .ToListAsync();

            return new ApiResponse<CompanyApplicationFiltersDTO>(200, new CompanyApplicationFiltersDTO
            {
                Statuses = statuses,
                Jobs = jobs
            });
        }

        public async Task<ApiResponse<CompanyApplicationFiltersDTO>> GetCompanyEmploymentRecordFiltersAsync(int employerId)
        {
            var historicalStatusIds = new[]
            {
                (int)ApplicationStatusEnum.Withdrawn,
                (int)ApplicationStatusEnum.MissingInterview,
                (int)ApplicationStatusEnum.Hired,
                (int)ApplicationStatusEnum.Rejected
            };

            var statuses = await _context.TbApplicationStatuses
                .Where(s => s.IsActive && historicalStatusIds.Contains(s.Id))
                .OrderBy(s => s.SortOrder)
                .Select(s => new LookUpDTO { Id = s.Id, Name = s.Name })
                .ToListAsync();

            var jobs = await _context.TbJobs
                .Where(j => j.EmployerId == employerId)
                .OrderBy(j => j.Title)
                .Select(j => new LookUpDTO { Id = j.Id, Name = j.Title })
                .ToListAsync();

            return new ApiResponse<CompanyApplicationFiltersDTO>(200, new CompanyApplicationFiltersDTO
            {
                Statuses = statuses,
                Jobs = jobs
            });
        }

        public async Task<ApiResponse<CompanyApplicationDetailsDTO>> GetCompanyApplicationDetailsAsync(int employerId, int applicationId)
        {
            var application = await _context.TbApplications
                .Include(a => a.Job)
                .Include(a => a.Seeker)
                .ThenInclude(s => s.ApplicationUser)
                .Include(a => a.ApplicationStatus)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

            if (application == null)
                return new ApiResponse<CompanyApplicationDetailsDTO>(404, "Application not found or unauthorized access.");

            var dto = new CompanyApplicationDetailsDTO
            {
                ApplicationId = application.Id,
                ProfilePhoto = application.Seeker.ProfilePhoto,
                FullName = (application.Seeker.FirsName + " " + application.Seeker.MiddleName + " " + application.Seeker.LastName).Replace("  ", " ").Trim(),
                Email = application.Seeker.ApplicationUser.Email ?? string.Empty,
                JobTitle = application.Job.Title,
                ApplicationDate = application.ApplicationDate,
                MatchingPercentage = application.MatchingPercentage,
                ApplicationStatus = application.ApplicationStatus.Name,
                CvDownloadUrl = application.Seeker.ResumeUrl,
                CanReject = application.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview
                         || application.ApplicationStatusId == (int)ApplicationStatusEnum.Shortlisted
                         || application.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed,
                CanSchedule = application.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview,
                CanHire = application.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed
            };

            if (!string.IsNullOrEmpty(dto.ProfilePhoto))
                dto.ProfilePhoto = _fileService.DownloadUrlAsync(dto.ProfilePhoto)?.SasUrl;

            if (!string.IsNullOrEmpty(dto.CvDownloadUrl))
                dto.CvDownloadUrl = _fileService.DownloadUrlAsync(dto.CvDownloadUrl)?.SasUrl;

            return new ApiResponse<CompanyApplicationDetailsDTO>(200, dto);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RejectApplicationAsync(int employerId, int applicationId)
        {
            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            // Only PendingReview, Shortlisted, or Interviewed can be rejected
            var allowedStatuses = new[]
            {
                (int)ApplicationStatusEnum.PendingReview,
                (int)ApplicationStatusEnum.Shortlisted,
                (int)ApplicationStatusEnum.Interviewed
            };

            if (!allowedStatuses.Contains(application.ApplicationStatusId))
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Application can only be rejected from PendingReview, Shortlisted, or Interviewed status.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Rejected;
            // Find related interview
            var interview = await _context.TbInterviews
                .FirstOrDefaultAsync(i => i.ApplicationId == application.Id);

            // Reject/Cancel related interview if exists
            if (interview != null)
            {
                if(interview.InterviewStatusId == (int)InterviewStatusEnum.Scheduled || interview.InterviewStatusId == (int)InterviewStatusEnum.Confirmed)
                {
                    interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;
                }
                
            }

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Application rejected successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> HireApplicationAsync(int employerId, int applicationId)
        {
            var appData = await _context.TbApplications
                .Where(a => a.Id == applicationId && a.Job.EmployerId == employerId)
                .Select(a => new
                {
                    Application = a,
                    CandidateEmail = a.Seeker.ApplicationUser.Email,
                    CandidateFirstName = a.Seeker.FirsName,
                    CandidateLastName = a.Seeker.LastName,
                    JobTitle = a.Job.Title,
                    CompanyName = a.Job.Employer.ComapnyName
                })
                .FirstOrDefaultAsync();

            if (appData == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (appData.Application.ApplicationStatusId != (int)ApplicationStatusEnum.Interviewed)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only applications in Interviewed status can be hired.");

            appData.Application.ApplicationStatusId = (int)ApplicationStatusEnum.Hired;
            await _context.SaveChangesAsync();

            try
            {
                var candidateName = $"{appData.CandidateFirstName} {appData.CandidateLastName}".Trim();
                if (!string.IsNullOrEmpty(appData.CandidateEmail))
                {
                    string emailSubject = "تهانينا! تم قبول طلب التوظيف الخاص بك";
                    string emailBody = BuildHiredEmailBody(candidateName, appData.JobTitle, appData.CompanyName);

                    await _emailService.SendEmailAsync(
                        appData.CandidateEmail,
                        emailSubject,
                        emailBody,
                        candidateName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending hire notification email: {ex.Message}");
            }

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Candidate hired successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> ScheduleInterviewAsync(
            int employerId, int applicationId, ScheduleInterviewRequestDTO dto)
        {
            var appData = await _context.TbApplications
                .Where(a => a.Id == applicationId && a.Job.EmployerId == employerId)
                .Select(a => new
                {
                    Application = a,
                    CandidateEmail = a.Seeker.ApplicationUser.Email,
                    CandidateUserName = a.Seeker.ApplicationUser.UserName,
                    CandidateFirstName = a.Seeker.FirsName,
                    CandidateLastName = a.Seeker.LastName,
                    JobTitle = a.Job.Title,
                    CompanyName = a.Job.Employer.ComapnyName
                })
                .FirstOrDefaultAsync();

            if (appData == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (appData.Application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only applications in PendingReview status can be scheduled for an interview.");

            var timeZoneInfo = GetTimeZoneFromHeader();
            var localDate = DateTime.SpecifyKind(dto.InterviewDate, DateTimeKind.Unspecified);
            var utcInterviewDate = TimeZoneInfo.ConvertTimeToUtc(localDate, timeZoneInfo);

            // Validate interview date is in the future
            if (utcInterviewDate <= DateTime.UtcNow)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview date must be in the future.");

            // Validate interview type exists
            var interviewTypeExists = await _context.TbInterviewTypes.AnyAsync(t => t.Id == dto.InterviewTypeId);
            if (!interviewTypeExists)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid interview type.");

            // Validate type-specific fields
            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
            {
                if (string.IsNullOrWhiteSpace(dto.MeetingLink))
                    return new ApiResponse<ConfirmationResponseDTO>(400,
                        "Meeting link is required for online interviews.");
            }
            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                if (!dto.CountryId.HasValue || !dto.GovernateId.HasValue || string.IsNullOrWhiteSpace(dto.AddressLine))
                    return new ApiResponse<ConfirmationResponseDTO>(400,
                        "Country, governate, and address line are required for in-person interviews.");

                // Validate country and governate exist
                var countryExists = await _context.TbCountries.AnyAsync(c => c.Id == dto.CountryId.Value);
                if (!countryExists)
                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid country.");

                var governateExists = await _context.TbGovernates
                    .AnyAsync(g => g.Id == dto.GovernateId.Value && g.CountryId == dto.CountryId.Value);
                if (!governateExists)
                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid governate for the selected country.");
            }

            // Create address for InPerson interviews
            int? addressId = null;

            if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                var address = new Address
                {
                    CountryId = dto.CountryId!.Value,
                    GovernateId = dto.GovernateId!.Value,
                    AddressLine1 = dto.AddressLine!
                };

                _context.TbAddresses.Add(address);

                await _context.SaveChangesAsync();

                addressId = address.Id;
            }

            // Create the interview
            var interview = new Interview
            {
                ApplicationId = applicationId,
                InterviewDate = utcInterviewDate,
                InterviewTypeId = dto.InterviewTypeId,
                AddressId = addressId,
                Notes = dto.Notes,
                MeetingLink = dto.InterviewTypeId == (int)InterviewTypeEnum.Online ? dto.MeetingLink : null,
                InterviewStatusId = (int)InterviewStatusEnum.Scheduled
            };
            _context.TbInterviews.Add(interview);

            // Update application status to Shortlisted
            appData.Application.ApplicationStatusId = (int)ApplicationStatusEnum.Shortlisted;

            await _context.SaveChangesAsync();

            var delay = utcInterviewDate - DateTime.UtcNow;
            var hangfireJobId = BackgroundJob.Schedule<InterviewExpirationService>(
                service => service.ExpireInterview(interview.Id),
                delay);

            interview.ExpirationHangfireJobId = hangfireJobId;
            await _context.SaveChangesAsync();

            // Send email to candidate
            if (!string.IsNullOrWhiteSpace(appData.CandidateEmail))
            {
                try
                {
                    string candidateName = $"{appData.CandidateFirstName} {appData.CandidateLastName}".Trim();
                    if (string.IsNullOrWhiteSpace(candidateName))
                    {
                        candidateName = appData.CandidateUserName ?? "Client";
                    }

                    string interviewType = dto.InterviewTypeId switch
                    {
                        (int)InterviewTypeEnum.Online => "عبر الإنترنت (Online)",
                        (int)InterviewTypeEnum.InPerson => "حضورياً (In-Person)",
                        (int)InterviewTypeEnum.Phone => "عبر الهاتف (Phone)",
                        _ => "جدول مقابلة"
                    };

                    string locationDetails = string.Empty;
                    if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
                    {
                        var country = await _context.TbCountries.FindAsync(dto.CountryId!.Value);
                        var governate = await _context.TbGovernates.FindAsync(dto.GovernateId!.Value);
                        locationDetails = $"{dto.AddressLine}, {governate?.Name}, {country?.Name}";
                    }
                    else if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
                    {
                        locationDetails = dto.MeetingLink ?? string.Empty;
                    }

                    string formattedDate = $"{utcInterviewDate:yyyy-MM-dd HH:mm} UTC";

                    string emailContent = BuildInterviewEmailBody(
                        candidateName,
                        appData.JobTitle,
                        appData.CompanyName,
                        interviewType,
                        formattedDate,
                        locationDetails,
                        dto.InterviewTypeId,
                        dto.Notes);

                    await _emailService.SendEmailAsync(
                        appData.CandidateEmail,
                        "تفاصيل المقابلة الشخصية المجدولة",
                        emailContent,
                        candidateName);
                }
                catch (Exception ex)
                {
                    // Log error or ignore to prevent the whole transaction from rolling back
                    // Since the interview was already saved in DB and status updated.
                    Console.WriteLine($"Error sending interview schedule email: {ex.Message}");
                }
            }

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview scheduled successfully." });
        }

        private string BuildInterviewEmailBody(
            string candidateName,
            string jobTitle,
            string companyName,
            string interviewType,
            string formattedDate,
            string? locationDetails,
            int interviewTypeId,
            string? notes)
        {
            string locationHtml = string.Empty;
            if (!string.IsNullOrWhiteSpace(locationDetails))
            {
                if (interviewTypeId == (int)InterviewTypeEnum.Online)
                {
                    locationHtml = $@"<p style=""margin: 5px 0;""><strong>رابط الاجتماع:</strong> <a href=""{locationDetails}"" target=""_blank"" style=""color: #0199db;"">{locationDetails}</a></p>";
                }
                else
                {
                    locationHtml = $@"<p style=""margin: 5px 0;""><strong>العنوان / التفاصيل:</strong> {locationDetails}</p>";
                }
            }

            string notesHtml = !string.IsNullOrWhiteSpace(notes)
                ? $@"<p style=""margin: 5px 0;""><strong>ملاحظات:</strong> {notes}</p>"
                : string.Empty;

            return $@"
            <div style=""direction: rtl; text-align: right; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; color: #4b5563; max-width: 600px; margin: auto; border: 1px solid #e5e7eb; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <h3 style=""color: #0199db; margin: 0;"">دعوة لمقابلة شخصية</h3>
                </div>
                <hr style=""border: 0; border-top: 1px solid #e5e7eb; margin-bottom: 20px;"" />
                <p>مرحباً <strong>{candidateName}</strong>،</p>
                <p>يسعدنا إبلاغك بأنه قد تم جدولة مقابلة شخصية لطلب التقديم الخاص بك لوظيفة <strong>{jobTitle}</strong> لدى شركة <strong>{companyName}</strong>.</p>
                
                <div style=""background-color: #f9fafb; border-right: 4px solid #02b5f1; padding: 15px; margin: 20px 0; border-radius: 8px;"">
                    <p style=""margin: 5px 0;""><strong>نوع المقابلة:</strong> {interviewType}</p>
                    <p style=""margin: 5px 0;""><strong>تاريخ ووقت المقابلة:</strong> {formattedDate}</p>
                    {locationHtml}
                    {notesHtml}
                </div>

                <p style=""font-size: 14px; color: #6b7280; margin-top: 20px;"">
                    نتمنى لك التوفيق والنجاح في مقابلتك القادمة.
                </p>
                
            </div>";
            //< p style = ""font - size: 12px; color: #9ca3af; text-align: center; margin-top: 30px; border-top: 1px solid #e5e7eb; padding-top: 15px;"">
            //        هذه رسالة تلقائية من منصة مسارك، الرجاء عدم الرد عليها مباشرة.
            //    </ p >
        }

        private string BuildHiredEmailBody(
            string candidateName,
            string jobTitle,
            string companyName)
        {
            return $@"
            <div style=""direction: rtl; text-align: right; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; color: #4b5563; max-width: 600px; margin: auto; border: 1px solid #e5e7eb; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <h3 style=""color: #0199db; margin: 0; font-size: 24px;"">تهانينا على قبولك! 🎉</h3>
                </div>
                <hr style=""border: 0; border-top: 1px solid #e5e7eb; margin-bottom: 20px;"" />
                <p>مرحباً <strong>{candidateName}</strong>،</p>
                <p>يسعدنا جداً إبلاغك بأنه قد تم قبولك وتعيينك لوظيفة <strong>{jobTitle}</strong> لدى شركة <strong>{companyName}</strong>.</p>
                
                <div style=""background-color: #ecfdf5; border-right: 4px solid #10b981; padding: 15px; margin: 20px 0; border-radius: 8px;"">
                    <p style=""margin: 5px 0; color: #065f46;""><strong>حالة الطلب:</strong> تم التوظيف (Hired)</p>
                    <p style=""margin: 5px 0; color: #065f46;""><strong>الوظيفة:</strong> {jobTitle}</p>
                    <p style=""margin: 5px 0; color: #065f46;""><strong>الشركة:</strong> {companyName}</p>
                </div>

                <p>سيقوم فريق الموارد البشرية لدى الشركة بالتواصل معك قريباً لمناقشة الخطوات التالية وتفاصيل العقد.</p>

                <p style=""font-size: 14px; color: #6b7280; margin-top: 20px;"">
                    نتمنى لك كل التوفيق والنجاح في مسيرتك المهنية الجديدة!
                </p>
            </div>";
        }
    }
}

