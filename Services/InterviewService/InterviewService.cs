using ECommerceApp.DTOs;
using GoWork.Authorization.Operations;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.CompanyInterviewDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;
using GoWork.Enums;
using GoWork.Models;
using GoWork.Services.CurrentUserService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using GoWork.Infrastructure.Hangfire;

namespace GoWork.Services.InterviewService
{
    public class InterviewService : IInterviewService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserService _currentUserService;

        public InterviewService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, ICurrentUserService currentUserService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _authorizationService = authorizationService;
            _currentUserService = currentUserService;
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

        public async Task<ApiResponse<ConfirmationResponseDTO>> HandleInterviewActionAsync(int interviewId, int userId, InterviewActionDTO dto)
        {
            // Resolve seeker from userId
            var seeker = await _context.TbSeekers
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (seeker == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "seeker not found");

            // Validate action value
            var action = dto.Action?.Trim();
            if (!string.Equals(action, "Confirm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid action. Use 'Confirm' or 'Cancel'.");
            }

            // Find interview and verify ownership through Application → Seeker
            //var interview = await _context.TbInterviews
            //    .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.SeekerId == seeker.Id);

            var interview = await _context.TbInterviews
            .Include(i => i.Application)
            .ThenInclude(a => a.Seeker)
            .FirstOrDefaultAsync(i => i.Id == interviewId);

            var result = await _authorizationService.AuthorizeAsync(_currentUserService.User, interview, "IsCandidateOwnInterviewPolicy");

            if (!result.Succeeded)
            {
                return new ApiResponse<ConfirmationResponseDTO>(403, "You are not authorized to perform this action on this interview.");
            }

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            // Enforce: only Scheduled interviews can be acted upon
            if (interview.InterviewStatusId != (int)Enums.InterviewStatusEnum.Scheduled)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview can only be accepted or cancelled when in Scheduled status.");



            // Map action to status
            //interview.InterviewStatusId = string.Equals(action, "Confirm", StringComparison.OrdinalIgnoreCase)
            //    ? (int)Enums.InterviewStatusEnum.Confirmed
            //    : (int)Enums.InterviewStatusEnum.Cancelled; 

            // CONFIRM
            if (string.Equals(action, "Confirm", StringComparison.OrdinalIgnoreCase))
            {
                interview.InterviewStatusId = (int)InterviewStatusEnum.Confirmed;
            }
            // CANCEL
            else
            {
                // Update interview status
                interview.InterviewStatusId = (int)InterviewStatusEnum.Withdrawn;

                // Update related application status
                interview.Application.ApplicationStatusId =
                    (int)ApplicationStatusEnum.Withdrawn;
            }



            interview.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var message = string.Equals(action, "Confirm", StringComparison.OrdinalIgnoreCase)
                ? "Interview confirmed successfully."
                : "Interview cancelled successfully.";

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = message });
        }

        public async Task<ApiResponse<InterviewResponseDTO>> GetCandidateInterviews(InterviewRequestDTO requestDTO)
        {
            if (!requestDTO.UserId.HasValue)
                return new ApiResponse<InterviewResponseDTO>(401, "unauthorized user");

            // Single async query for the seeker
            var seeker = await _context.TbSeekers
                .FirstOrDefaultAsync(s => s.UserId == requestDTO.UserId.Value);

            if (seeker == null)
                return new ApiResponse<InterviewResponseDTO>(404, "seeker not found");

            // Build base query: interviews whose application belongs to the seeker
            var baseQuery = _context.TbInterviews
                .Where(i => i.Application.SeekerId == seeker.Id);

            // Apply optional status filter
            if (requestDTO.InterviewStatusId.HasValue)
            {
                baseQuery = baseQuery.Where(i => i.InterviewStatusId == requestDTO.InterviewStatusId.Value);
            }

            // Apply sorting (default: newest first)
            baseQuery = string.Equals(requestDTO.SortOrder, "asc", StringComparison.OrdinalIgnoreCase)
                ? baseQuery.OrderBy(i => i.InterviewDate)
                : baseQuery.OrderByDescending(i => i.InterviewDate);

            // Count BEFORE pagination
            var totalCount = await baseQuery.CountAsync();

            // Apply pagination
            var pagedInterviews = await baseQuery
                .Skip((requestDTO.Page - 1) * requestDTO.PageSize)
                .Take(requestDTO.PageSize)
                .Select(i => new InterviewDTO
                {
                    Id = i.Id,
                    JobTitle = i.Application.Job.Title,
                    CompanyName = i.Application.Job.Employer.ComapnyName,
                    // Adding 3 hours until osama solve the problem of time zone
                    InterviewDate = i.InterviewDate.AddHours(3),
                    InterviewType = i.InterviewType.Name,
                    Location = i.Address != null
                        ? $"{i.Address.AddressLine1}, {i.Address.Governate.Name}, {i.Address.Country.Name}"
                        : null,
                    MeetingLink = i.MeetingLink ?? (i.InterviewTypeId != (int)Enums.InterviewTypeEnum.InPerson
                        ? "Meeting link not provided yet"
                        : null),
                    Notes = i.Notes,
                    Status = i.InterviewStatus.Name
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalCount / requestDTO.PageSize);

            return new ApiResponse<InterviewResponseDTO>(200, new InterviewResponseDTO
            {
                Interviews = pagedInterviews,
                PageNumber = requestDTO.Page,
                PageSize = requestDTO.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = requestDTO.Page < totalPages
            });
        }


        // ======================== COMPANY DASHBOARD METHODS ========================

        private IQueryable<Interview> FilterCompanyActiveInterviews(IQueryable<Interview> query)
        {
            var today = DateTime.UtcNow.Date;

            return query.Where(i =>
                !(
                    (
                        i.Application.ApplicationStatusId == (int)ApplicationStatusEnum.Withdrawn &&
                        i.Application.ApplicationDate.Date < today
                    )
                    ||
                    (
                        i.Application.ApplicationStatusId == (int)ApplicationStatusEnum.MissingInterview &&
                        i.Application.Interviews
                            .OrderByDescending(x => x.InterviewDate)
                            .Select(x => (DateTime?)x.InterviewDate.Date)
                            .FirstOrDefault() < today
                    )
                    ||
                    (
                        (i.Application.ApplicationStatusId == (int)ApplicationStatusEnum.Rejected ||
                         i.Application.ApplicationStatusId == (int)ApplicationStatusEnum.Hired) &&
                        (
                            (
                                i.Application.Interviews.Any() &&
                                i.Application.Interviews
                                    .OrderByDescending(x => x.InterviewDate)
                                    .Select(x => (DateTime?)x.InterviewDate.Date)
                                    .FirstOrDefault() < today
                            )
                            ||
                            (
                                !i.Application.Interviews.Any() &&
                                i.Application.ApplicationDate.Date < today
                            )
                        )
                    )
                )
            );
        }

        public async Task<ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>> GetCompanyInterviewsAsync(
    int employerId, CompanyInterviewsRequestDTO request)
        {
            var now = DateTime.UtcNow;

            // Auto-update past interviews to MissingInterview
            //var pastInterviews = await _context.TbInterviews
            //    .Include(i => i.Application)
            //    .Where(i => i.Application.Job.EmployerId == employerId &&
            //                (i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled ) &&
            //                i.InterviewDate <= now)
            //    .ToListAsync();

            //if (pastInterviews.Any())
            //{
            //    foreach (var interview in pastInterviews)
            //    {
            //        interview.InterviewStatusId = (int)InterviewStatusEnum.MissingInterview;
            //        interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.MissingInterview;
            //    }
            //    await _context.SaveChangesAsync();
            //}

            // Base query: all interviews for this employer's jobs
            var baseQuery = _context.TbInterviews
                .Where(i => i.Application.Job.EmployerId == employerId);
            baseQuery = FilterCompanyActiveInterviews(baseQuery);

            // Search filter: by candidate name or job title
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();

                baseQuery = baseQuery.Where(i =>
                    i.Application.Job.Title.ToLower().Contains(search) ||

                    (i.Application.Seeker.FirsName + " " +
                     i.Application.Seeker.MiddleName + " " +
                     i.Application.Seeker.LastName)
                     .ToLower()
                     .Contains(search)
                );
            }

            // Filter by interview status
            if (request.InterviewStatusId.HasValue)
            {
                baseQuery = baseQuery.Where(i =>
                    i.InterviewStatusId == request.InterviewStatusId.Value);
            }

            // Filter by job
            if (request.JobId.HasValue)
            {
                baseQuery = baseQuery.Where(i =>
                    i.Application.JobId == request.JobId.Value);
            }

            // Order interviews:
            // 1. Upcoming/today interviews first
            // 2. Past interviews last
            // 3. Nearest upcoming interview first
            baseQuery = baseQuery
                .OrderBy(i => i.InterviewDate < now)
                .ThenBy(i => i.InterviewDate);

            // Count before pagination
            var totalCount = await baseQuery.CountAsync();

            // Paginate and project
            var items = await baseQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new CompanyInterviewListItemDTO
                {
                    InterviewId = i.Id,
                    ApplicationId = i.ApplicationId,

                    CandidateName =
                        i.Application.Seeker.FirsName + " " +
                        i.Application.Seeker.MiddleName + " " +
                        i.Application.Seeker.LastName,

                    JobTitle = i.Application.Job.Title,

                    InterviewDate = i.InterviewDate,

                    InterviewType = i.InterviewType.Name,

                    InterviewStatus = i.InterviewStatus.Name,

                    Notes = i.Notes,

                    Email = i.Application.Seeker.ApplicationUser.Email,

                    CountryId = i.Address.CountryId,

                    GovernateId = i.Address.GovernateId,

                    AddressLine = i.Address.AddressLine1,

                    AddressId = i.AddressId,

                    // Location
                    Location = i.InterviewTypeId == (int)InterviewTypeEnum.InPerson
                        ? (i.Address != null
                            ? i.Address.Country.Name + ", " +
                              i.Address.Governate.Name + ", " +
                              i.Address.AddressLine1
                            : null)
                        : i.MeetingLink,

                    // Action flags

                    CanCancel =
                        (
                            i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled
                            && i.InterviewDate > now
                        )
                        ||
                        (
                            i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                            && i.InterviewDate > now
                        )
                        ||
                        (
                            i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled
                            && i.InterviewDate <= now
                        ),

                    CanReschedule =
                        i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled
                        && i.InterviewDate > now,

                    CanComplete =
                        i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                        && i.InterviewDate <= now,

                    CanMarkMissing =
                        i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                        && i.InterviewDate <= now
                })
                .ToListAsync();

            // Convert UTC dates to client timezone
            var timeZoneInfo = GetTimeZoneFromHeader();

            foreach (var item in items)
            {
                var utcDate = DateTime.SpecifyKind(
                    item.InterviewDate,
                    DateTimeKind.Utc);

                item.InterviewDate =
                    TimeZoneInfo.ConvertTimeFromUtc(
                        utcDate,
                        timeZoneInfo);
            }

            return new ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>(
                200,
                new PaginatedResult<CompanyInterviewListItemDTO>
                {
                    Items = items,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = totalCount
                });
        }

        public async Task<ApiResponse<CompanyInterviewFiltersDTO>> GetCompanyInterviewFiltersAsync(int employerId)
        {
            // Get interview statuses
            //var statuses = await _context.TbInterviewStatuses
            //    .OrderBy(s => s.SortOrder)
            //    .Select(s => new LookUpDTO { Id = s.Id, Name = s.Name })
            //    .ToListAsync();

            //var statuses = await _context.TbInterviewStatuses
            //.Where(s =>
            //    s.Id == (int)InterviewStatusEnum.Scheduled ||
            //    s.Id == (int)InterviewStatusEnum.Completed || 
            //    s.Id == (int) InterviewStatusEnum.Confirmed || 
            //    s.Id == (int) InterviewStatusEnum.MissingInterview ||
            //    s.Id == (int) InterviewStatusEnum.Cancelled || 
            //    s.Id == (int)InterviewStatusEnum.Withdrawn)
            //.OrderBy(s => s.SortOrder)
            //.Select(s => new LookUpDTO
            //{
            //    Id = s.Id,
            //    Name = s.Name
            //})
            //.ToListAsync();

            var statuses = await _context.TbInterviewStatuses
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

            return new ApiResponse<CompanyInterviewFiltersDTO>(200, new CompanyInterviewFiltersDTO
            {
                Statuses = statuses,
                Jobs = jobs
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> CancelInterviewAsync(int employerId, int interviewId)
        {
            var interview = await _context.TbInterviews
                .Include(i => i.Application)
                    .ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employerId);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            // Only Scheduled or Confirmed (when date ≠ today) can be cancelled
            var isScheduled = interview.InterviewStatusId == (int)InterviewStatusEnum.Scheduled;

            //var timeZoneInfo = GetTimeZoneFromHeader();
            //var localInterviewDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(interview.InterviewDate, DateTimeKind.Utc), timeZoneInfo);
            //var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);

            var isConfirmedNotToday = interview.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                                   && interview.InterviewDate != DateTime.UtcNow;

            if (!isScheduled && !isConfirmedNotToday)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Interview can only be cancelled when in Scheduled status, or Confirmed status with a future date.");

            // Cancel the interview
            interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;

            // Delete Hangfire job if it exists
            if (!string.IsNullOrEmpty(interview.ExpirationHangfireJobId))
            {
                BackgroundJob.Delete(interview.ExpirationHangfireJobId);
                interview.ExpirationHangfireJobId = null;
            }

            // Per workflow: cancelling an interview rejects the application
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.Rejected;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview cancelled and application rejected." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RescheduleInterviewAsync(
            int employerId, int interviewId, RescheduleInterviewRequestDTO dto)
        {
            var interview = await _context.TbInterviews
                .Include(i => i.Application)
                    .ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employerId);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewStatusId != (int)InterviewStatusEnum.Scheduled)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only interviews in Scheduled status can be rescheduled.");

            // Validate interview date is in the future
            var timeZoneInfo = GetTimeZoneFromHeader();
            var localDate = DateTime.SpecifyKind(dto.InterviewDate, DateTimeKind.Unspecified);
            var utcInterviewDate = TimeZoneInfo.ConvertTimeToUtc(localDate, timeZoneInfo);

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

                var countryExists = await _context.TbCountries.AnyAsync(c => c.Id == dto.CountryId.Value);
                if (!countryExists)
                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid country.");

                var governateExists = await _context.TbGovernates
                    .AnyAsync(g => g.Id == dto.GovernateId.Value && g.CountryId == dto.CountryId.Value);
                if (!governateExists)
                    return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid governate for the selected country.");
            }

            // Update interview details
            interview.InterviewDate = utcInterviewDate;
            interview.InterviewTypeId = dto.InterviewTypeId;
            interview.Notes = dto.Notes;
            interview.RespondedAt = null; // Reset candidate response

            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
            {
                interview.MeetingLink = dto.MeetingLink;
                interview.AddressId = null; // Clear address for online interview
                var currentAddress = await _context.TbAddresses.FindAsync(interview.AddressId);
                if (currentAddress != null)
                {
                    _context.TbAddresses.Remove(currentAddress);
                }
            }
            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                interview.MeetingLink = null;

                var CurrentAddress = _context.TbAddresses.Find(dto.AddressId);

                if (CurrentAddress != null)
                {
                    CurrentAddress.CountryId = dto.CountryId.Value;
                    CurrentAddress.GovernateId = dto.GovernateId.Value;
                    CurrentAddress.AddressLine1 = dto.AddressLine;
                }
            }
            else
            {
                // Phone interview
                interview.MeetingLink = dto.MeetingLink;
            }

            // Keep status as Scheduled (candidate must re-confirm)
            interview.InterviewStatusId = (int)InterviewStatusEnum.Scheduled;

            if (!string.IsNullOrEmpty(interview.ExpirationHangfireJobId))
            {
                BackgroundJob.Delete(interview.ExpirationHangfireJobId);
            }

            var delay = utcInterviewDate - DateTime.UtcNow;
            var newHangfireJobId = BackgroundJob.Schedule<InterviewExpirationService>(
                service => service.ExpireInterview(interview.Id),
                delay);

            interview.ExpirationHangfireJobId = newHangfireJobId;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview rescheduled successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> CompleteInterviewAsync(int employerId, int interviewId)
        {
            var interview = await _context.TbInterviews
                .Include(i => i.Application)
                    .ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employerId);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewStatusId != (int)InterviewStatusEnum.Confirmed)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only confirmed interviews can be marked as completed.");

            //var timeZoneInfo = GetTimeZoneFromHeader();
            //var localInterviewDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(interview.InterviewDate, DateTimeKind.Utc), timeZoneInfo);
            //var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);

            //if (localInterviewDate.Date != localNow.Date)
            //    return new ApiResponse<ConfirmationResponseDTO>(400,
            //        "Interview can only be marked as completed on the interview date.");

            // Complete the interview
            interview.InterviewStatusId = (int)InterviewStatusEnum.Completed;

            // Update application to Interviewed
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.Interviewed;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview marked as completed." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkMissingInterviewAsync(int employerId, int interviewId)
        {
            var interview = await _context.TbInterviews
                .Include(i => i.Application)
                    .ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employerId);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewStatusId != (int)InterviewStatusEnum.Confirmed)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Only confirmed interviews can be marked as missing.");

            //var timeZoneInfo = GetTimeZoneFromHeader();
            //var localInterviewDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(interview.InterviewDate, DateTimeKind.Utc), timeZoneInfo);
            //var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);

            //if (localInterviewDate.Date != localNow.Date)
            //    return new ApiResponse<ConfirmationResponseDTO>(400,
            //        "Interview can only be marked as missing on the interview date.");

            // Mark interview as missing
            interview.InterviewStatusId = (int)InterviewStatusEnum.MissingInterview;

            // Update application to MissingInterview
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.MissingInterview;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview marked as missing." });
        }
    }
}
