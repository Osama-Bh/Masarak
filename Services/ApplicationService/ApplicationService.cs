//using ECommerceApp.DTOs;
//using GoWork.Authorization.Operations;
//using GoWork.Data;
//using GoWork.DTOs;
//using GoWork.DTOs.ApplicationDTOs;
//using GoWork.DTOs.CompanyApplicationDTOs;
//using GoWork.DTOs.DashboardDTOs;
//using GoWork.Enums;
//using GoWork.Models;
//using GoWork.Services.CurrentUserService;
//using GoWork.Services.FileService;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.EntityFrameworkCore;

//namespace GoWork.Services.ApplicationService
//{
//    public class ApplicationService : IApplicationService
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IFileService _fileService;
//        private readonly ICurrentUserService _currentUserService;
//        private readonly IAuthorizationService _authorizationService;


//        public ApplicationService(ApplicationDbContext context, IFileService fileService, ICurrentUserService currentUserService, IAuthorizationService authorizationService)
//        {
//            _context = context;
//            _fileService = fileService;
//            _currentUserService = currentUserService;
//            _authorizationService = authorizationService;
//        }

//        public async Task<ApiResponse<List<LookUpDTO>>> GetApplicationStatuses()
//        {
//            var items = await _context.TbApplicationStatuses
//                .Where(a => a.IsActive)
//                .OrderBy(a => a.SortOrder)
//                .Select(a => new LookUpDTO
//                {
//                    Id = a.Id,
//                    Name = a.Name
//                })
//                .ToListAsync();

//            return new ApiResponse<List<LookUpDTO>>(200, items);
//        }

//        public async Task<ApiResponse<ApplicationsResponseDTO>> GetCandidateApplications(ApplicationsRequestDTO requestDTO)
//        {
//            if (!requestDTO.UserId.HasValue)
//                return new ApiResponse<ApplicationsResponseDTO>(401, "unauthorized user");

//            // Single async query for the seeker
//            var seeker = await _context.TbSeekers
//                .FirstOrDefaultAsync(s => s.UserId == requestDTO.UserId.Value);

//            if (seeker == null)
//                return new ApiResponse<ApplicationsResponseDTO>(404, "seeker not found");


//            // Build base query filtered by seeker
//            var baseQuery = _context.TbApplications.Where(a => a.SeekerId == seeker.Id && a.ApplicationStatusId != (int)ApplicationStatusEnum.Withdrawn);

//            // Apply optional status filter
//            if (requestDTO.ApplicationStatusId.HasValue)
//            {
//                baseQuery = baseQuery.Where(a => a.ApplicationStatusId == requestDTO.ApplicationStatusId.Value);
//            }

//            // Apply sorting (default: newest first)
//            baseQuery = string.Equals(requestDTO.SortOrder, "desc", StringComparison.OrdinalIgnoreCase)
//                ? baseQuery.OrderByDescending(a => a.ApplicationDate)
//                : baseQuery.OrderBy(a => a.ApplicationDate);

//            // Count BEFORE pagination
//            var totalCount = await baseQuery.CountAsync();

//            // Apply pagination
//            var pagedApplications = await baseQuery
//                .Skip((requestDTO.Page - 1) * requestDTO.PageSize)
//                .Take(requestDTO.PageSize)
//                .Select(a => new ApplicationDTO
//                {
//                    ApplicationId = a.Id,
//                    JobId = a.JobId,
//                    JobTitle = a.Job.Title,
//                    CompanyName = a.Job.Employer.ComapnyName,
//                    CompanyLogo = a.Job.Employer.LogoUrl,
//                    ApplicationStatus = a.ApplicationStatus.Name,
//                    AppliedDate = a.ApplicationDate,
//                    CanWithdraw = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview
//                })
//                .ToListAsync();

//            // Resolve company logo URLs via file service
//            foreach (var application in pagedApplications)
//            {
//                if (!string.IsNullOrEmpty(application.CompanyLogo))
//                    application.CompanyLogo = _fileService.DownloadUrlAsync(application.CompanyLogo)?.SasUrl;
//                else
//                    application.CompanyLogo = null;
//            }

//            var totalPages = (int)Math.Ceiling((double)totalCount / requestDTO.PageSize);

//            return new ApiResponse<ApplicationsResponseDTO>(200, new ApplicationsResponseDTO
//            {
//                Applications = pagedApplications,
//                PageNumber = requestDTO.Page,
//                PageSize = requestDTO.PageSize,
//                TotalCount = totalCount,
//                TotalPages = totalPages,
//                HasNextPage = requestDTO.Page < totalPages
//            });
//        }

//        public async Task<ApiResponse<ConfirmationResponseDTO>> WithdrawApplicationAsync(int applicationId, int userId)
//        {
//            var seeker = await _context.TbSeekers
//                .FirstOrDefaultAsync(s => s.UserId == userId);

//            if (seeker == null)
//                return new ApiResponse<ConfirmationResponseDTO>(404, "seeker not found");


//            var application = await _context.TbApplications
//                .FirstOrDefaultAsync(a => a.Id == applicationId);

//            if (application == null)
//                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

//            var authorizationResult = await _authorizationService.AuthorizeAsync(_currentUserService.User, application, 
//                ApplicationOperations.Withdraw);

//            if (!authorizationResult.Succeeded)
//                return new ApiResponse<ConfirmationResponseDTO>(403, "You are not authorized to withdraw this application.");

//            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
//                return new ApiResponse<ConfirmationResponseDTO>(400, "Only applications under pending review can be withdrawn.");

//            application.ApplicationStatusId = (int)ApplicationStatusEnum.Withdrawn;
//            await _context.SaveChangesAsync();

//            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Application withdrawn successfully." });
//        }

//        // ======================== COMPANY DASHBOARD METHODS ========================

//        public async Task<ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>> GetCompanyApplicationsAsync(
//            int employerId, CompanyApplicationsRequestDTO request)
//        {
//            // Base query: all applications for this employer's jobs
//            var baseQuery = _context.TbApplications
//                .Where(a => a.Job.EmployerId == employerId);

//            // Search filter: by job title or candidate name
//            if (!string.IsNullOrWhiteSpace(request.Search))
//            {
//                var search = request.Search.Trim().ToLower();
//                baseQuery = baseQuery.Where(a =>
//                    a.Job.Title.ToLower().Contains(search) ||
//                    (a.Seeker.FirsName + " " + a.Seeker.MiddleName + " " + a.Seeker.LastName).ToLower().Contains(search)
//                );
//            }

//            // Filter by application status
//            if (request.ApplicationStatusId.HasValue)
//            {
//                baseQuery = baseQuery.Where(a => a.ApplicationStatusId == request.ApplicationStatusId.Value);
//            }

//            // Filter by job
//            if (request.JobId.HasValue)
//            {
//                baseQuery = baseQuery.Where(a => a.JobId == request.JobId.Value);
//            }

//            // Sort by newest first
//            baseQuery = baseQuery.OrderByDescending(a => a.ApplicationDate);

//            // Count before pagination
//            var totalCount = await baseQuery.CountAsync();

//            // Paginate and project
//            var items = await baseQuery
//                .Skip((request.Page - 1) * request.PageSize)
//                .Take(request.PageSize)
//                .Select(a => new CompanyApplicationListItemDTO
//                {
//                    ApplicationId = a.Id,
//                    ProfilePhoto = a.Seeker.ProfilePhoto,
//                    FullName = a.Seeker.FirsName + " " + a.Seeker.MiddleName + " " + a.Seeker.LastName,
//                    Email = a.Seeker.ApplicationUser.Email ?? string.Empty,
//                    JobTitle = a.Job.Title,
//                    ApplicationDate = a.ApplicationDate,
//                    MatchingPercentage = a.MatchingPercentage,
//                    ApplicationStatus = a.ApplicationStatus.Name,
//                    CvDownloadUrl = a.Seeker.ResumeUrl,

//                    // Action flags based on status
//                    CanReject = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview
//                             || a.ApplicationStatusId == (int)ApplicationStatusEnum.Shortlisted
//                             || a.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed,
//                    CanSchedule = a.ApplicationStatusId == (int)ApplicationStatusEnum.PendingReview,
//                    CanHire = a.ApplicationStatusId == (int)ApplicationStatusEnum.Interviewed
//                })
//                .ToListAsync();

//            // Resolve file URLs (profile photo and CV) via FileService
//            foreach (var item in items)
//            {
//                if (!string.IsNullOrEmpty(item.ProfilePhoto))
//                    item.ProfilePhoto = _fileService.DownloadUrlAsync(item.ProfilePhoto)?.SasUrl;

//                if (!string.IsNullOrEmpty(item.CvDownloadUrl))
//                    item.CvDownloadUrl = _fileService.DownloadUrlAsync(item.CvDownloadUrl)?.SasUrl;
//            }

//            return new ApiResponse<PaginatedResult<CompanyApplicationListItemDTO>>(200,
//                new PaginatedResult<CompanyApplicationListItemDTO>
//                {
//                    Items = items,
//                    CurrentPage = request.Page,
//                    PageSize = request.PageSize,
//                    TotalCount = totalCount
//                });
//        }

//        public async Task<ApiResponse<CompanyApplicationFiltersDTO>> GetCompanyApplicationFiltersAsync(int employerId)
//        {
//            // Get application statuses
//            var statuses = await _context.TbApplicationStatuses
//                .Where(s => s.IsActive)
//                .OrderBy(s => s.SortOrder)
//                .Select(s => new LookUpDTO { Id = s.Id, Name = s.Name })
//                .ToListAsync();

//            // Get employer's jobs for the job filter dropdown
//            var jobs = await _context.TbJobs
//                .Where(j => j.EmployerId == employerId)
//                .OrderBy(j => j.Title)
//                .Select(j => new LookUpDTO { Id = j.Id, Name = j.Title })
//                .ToListAsync();

//            return new ApiResponse<CompanyApplicationFiltersDTO>(200, new CompanyApplicationFiltersDTO
//            {
//                Statuses = statuses,
//                Jobs = jobs
//            });
//        }

//        public async Task<ApiResponse<ConfirmationResponseDTO>> RejectApplicationAsync(int employerId, int applicationId)
//        {
//            var application = await _context.TbApplications
//                .Include(a => a.Job)
//                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

//            if (application == null)
//                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

//            // Only PendingReview, Shortlisted, or Interviewed can be rejected
//            var allowedStatuses = new[]
//            {
//                (int)ApplicationStatusEnum.PendingReview,
//                (int)ApplicationStatusEnum.Shortlisted,
//                (int)ApplicationStatusEnum.Interviewed
//            };

//            if (!allowedStatuses.Contains(application.ApplicationStatusId))
//                return new ApiResponse<ConfirmationResponseDTO>(400,
//                    "Application can only be rejected from PendingReview, Shortlisted, or Interviewed status.");

//            application.ApplicationStatusId = (int)ApplicationStatusEnum.Rejected;
//            // Find related interview
//            var interview = await _context.TbInterviews
//                .FirstOrDefaultAsync(i => i.ApplicationId == application.Id);

//            // Reject/Cancel related interview if exists
//            if (interview != null)
//            {
//                interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;
//            }

//            await _context.SaveChangesAsync();

//            return new ApiResponse<ConfirmationResponseDTO>(200,
//                new ConfirmationResponseDTO { Message = "Application rejected successfully." });
//        }

//        public async Task<ApiResponse<ConfirmationResponseDTO>> HireApplicationAsync(int employerId, int applicationId)
//        {
//            var application = await _context.TbApplications
//                .Include(a => a.Job)
//                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

//            if (application == null)
//                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

//            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.Interviewed)
//                return new ApiResponse<ConfirmationResponseDTO>(400,
//                    "Only applications in Interviewed status can be hired.");

//            application.ApplicationStatusId = (int)ApplicationStatusEnum.Hired;
//            await _context.SaveChangesAsync();

//            return new ApiResponse<ConfirmationResponseDTO>(200,
//                new ConfirmationResponseDTO { Message = "Candidate hired successfully." });
//        }

//        public async Task<ApiResponse<ConfirmationResponseDTO>> ScheduleInterviewAsync(
//            int employerId, int applicationId, ScheduleInterviewRequestDTO dto)
//        {
//            var application = await _context.TbApplications
//                .Include(a => a.Job)
//                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

//            if (application == null)
//                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

//            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
//                return new ApiResponse<ConfirmationResponseDTO>(400,
//                    "Only applications in PendingReview status can be scheduled for an interview.");

//            // Validate interview date is in the future
//            if (dto.InterviewDate <= DateTime.UtcNow)
//                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview date must be in the future.");

//            // Validate interview type exists
//            var interviewTypeExists = await _context.TbInterviewTypes.AnyAsync(t => t.Id == dto.InterviewTypeId);
//            if (!interviewTypeExists)
//                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid interview type.");

//            // Validate type-specific fields
//            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
//            {
//                if (string.IsNullOrWhiteSpace(dto.MeetingLink))
//                    return new ApiResponse<ConfirmationResponseDTO>(400,
//                        "Meeting link is required for online interviews.");
//            }
//            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
//            {
//                if (!dto.CountryId.HasValue || !dto.GovernateId.HasValue || string.IsNullOrWhiteSpace(dto.AddressLine))
//                    return new ApiResponse<ConfirmationResponseDTO>(400,
//                        "Country, governate, and address line are required for in-person interviews.");

//                // Validate country and governate exist
//                var countryExists = await _context.TbCountries.AnyAsync(c => c.Id == dto.CountryId.Value);
//                if (!countryExists)
//                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid country.");

//                var governateExists = await _context.TbGovernates
//                    .AnyAsync(g => g.Id == dto.GovernateId.Value && g.CountryId == dto.CountryId.Value);
//                if (!governateExists)
//                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid governate for the selected country.");
//            }

//            // Create address for InPerson interviews
//            int? addressId = null;

//            if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
//            {
//                var address = new Address
//                {
//                    CountryId = dto.CountryId!.Value,
//                    GovernateId = dto.GovernateId!.Value,
//                    AddressLine1 = dto.AddressLine!
//                };

//                _context.TbAddresses.Add(address);

//                await _context.SaveChangesAsync();

//                addressId = address.Id;
//            }

//            // Create the interview
//            var interview = new Interview
//            {
//                ApplicationId = applicationId,
//                InterviewDate = dto.InterviewDate,
//                InterviewTypeId = dto.InterviewTypeId,
//                AddressId = addressId,
//                Notes = dto.Notes,
//                MeetingLink = dto.InterviewTypeId == (int)InterviewTypeEnum.Online? dto.MeetingLink: null,
//                InterviewStatusId = (int)InterviewStatusEnum.Scheduled
//            };
//            _context.TbInterviews.Add(interview);

//            // Update application status to Shortlisted
//            application.ApplicationStatusId = (int)ApplicationStatusEnum.Shortlisted;

//            await _context.SaveChangesAsync();

//            return new ApiResponse<ConfirmationResponseDTO>(200,
//                new ConfirmationResponseDTO { Message = "Interview scheduled successfully." });
//        }
//    }
//}

using ECommerceApp.DTOs;
using GoWork.Authorization.Operations;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.CompanyApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Models;
using GoWork.Services.CurrentUserService;
using GoWork.Services.FileService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.ApplicationService
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public ApplicationService(ApplicationDbContext context, IFileService fileService, ICurrentUserService currentUserService, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _fileService = fileService;
            _currentUserService = currentUserService;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
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
            var baseQuery = _context.TbApplications.Where(a => a.SeekerId == seeker.Id && a.ApplicationStatusId != (int)ApplicationStatusEnum.Withdrawn);

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


            var application = await _context.TbApplications
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
                .Where(s => s.IsActive &&
                            s.Id != (int)ApplicationStatusEnum.Withdrawn &&
                            s.Id != (int)ApplicationStatusEnum.MissingInterview &&
                            s.Id != (int)ApplicationStatusEnum.Rejected &&
                            s.Id != (int)ApplicationStatusEnum.Hired)
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
            //// Find related interview
            //var interview = await _context.TbInterviews
            //    .FirstOrDefaultAsync(i => i.ApplicationId == application.Id);

            //// Reject/Cancel related interview if exists
            //if (interview != null)
            //{
            //    interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;
            //}

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Application rejected successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> HireApplicationAsync(int employerId, int applicationId)
        {
            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.Interviewed)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only applications in Interviewed status can be hired.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Hired;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Candidate hired successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> ScheduleInterviewAsync(
            int employerId, int applicationId, ScheduleInterviewRequestDTO dto)
        {
            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employerId);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
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
            application.ApplicationStatusId = (int)ApplicationStatusEnum.Shortlisted;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview scheduled successfully." });
        }
    }
}

