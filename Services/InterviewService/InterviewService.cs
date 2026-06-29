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
using GoWork.Services.EmailService;

namespace GoWork.Services.InterviewService
{
    public class InterviewService : IInterviewService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _emailService;

        public InterviewService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, ICurrentUserService currentUserService, IEmailService emailService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _authorizationService = authorizationService;
            _currentUserService = currentUserService;
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
                    InterviewDate = i.InterviewDate,
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
            var interviewData = await _context.TbInterviews
            .Where(i => i.Id == interviewId &&
                        i.Application.Job.EmployerId == employerId)
            .Select(i => new
            {
                Interview = i,
                CandidateEmail = i.Application.Seeker.ApplicationUser.Email,
                CandidateName = i.Application.Seeker.FirsName + " " + i.Application.Seeker.LastName,
                JobTitle = i.Application.Job.Title,
                EmployerName = i.Application.Job.Employer.ComapnyName
            })
            .FirstOrDefaultAsync();

            if (interviewData == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interviewData.Interview.InterviewStatusId != (int)InterviewStatusEnum.Scheduled)
                return new ApiResponse<ConfirmationResponseDTO>(
                    400,
                    "Only interviews in Scheduled status can be rescheduled.");

            // Convert local interview time to UTC
            var timeZoneInfo = GetTimeZoneFromHeader();
            var localDate = DateTime.SpecifyKind(dto.InterviewDate, DateTimeKind.Unspecified);
            var utcInterviewDate = TimeZoneInfo.ConvertTimeToUtc(localDate, timeZoneInfo);

            if (utcInterviewDate <= DateTime.UtcNow)
                return new ApiResponse<ConfirmationResponseDTO>(
                    400,
                    "Interview date must be in the future.");

            if (!Enum.IsDefined(typeof(InterviewTypeEnum), dto.InterviewTypeId))
                return new ApiResponse<ConfirmationResponseDTO>(
                    400,
                    "Invalid interview type.");

            string? countryName = null;
            string? governateName = null;

            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
            {
                if (string.IsNullOrWhiteSpace(dto.MeetingLink))
                    return new ApiResponse<ConfirmationResponseDTO>(
                        400,
                        "Meeting link is required for online interviews.");
            }
            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                if (!dto.CountryId.HasValue ||
                    !dto.GovernateId.HasValue ||
                    string.IsNullOrWhiteSpace(dto.AddressLine))
                {
                    return new ApiResponse<ConfirmationResponseDTO>(
                        400,
                        "Country, governate, and address line are required for in-person interviews.");
                }

                var location = await _context.TbGovernates
                    .Where(g => g.Id == dto.GovernateId &&
                                g.CountryId == dto.CountryId)
                    .Select(g => new
                    {
                        Governate = g.Name,
                        Country = g.Country.Name
                    })
                    .FirstOrDefaultAsync();

                if (location == null)
                    return new ApiResponse<ConfirmationResponseDTO>(
                        400,
                        "Invalid governate for the selected country.");

                governateName = location.Governate;
                countryName = location.Country;
            }

            interviewData.Interview.InterviewDate = utcInterviewDate;
            interviewData.Interview.InterviewTypeId = dto.InterviewTypeId;
            interviewData.Interview.Notes = dto.Notes;
            interviewData.Interview.RespondedAt = null;
            interviewData.Interview.InterviewStatusId = (int)InterviewStatusEnum.Scheduled;

            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
            {
                interviewData.Interview.MeetingLink = dto.MeetingLink;

                if (interviewData.Interview.AddressId.HasValue)
                {
                    var currentAddress = await _context.TbAddresses.FindAsync(interviewData.Interview.AddressId);
                    if (currentAddress != null)
                    {
                        _context.TbAddresses.Remove(currentAddress);
                    }
                    interviewData.Interview.AddressId = null; // Clear address for online interview
                }
            }
            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                interviewData.Interview.MeetingLink = null;

                if(interviewData.Interview.AddressId.HasValue)
                {
                    var currentAddress = await _context.TbAddresses.FindAsync(interviewData.Interview.AddressId);
                    if (currentAddress != null)
                    {
                        currentAddress.CountryId = dto.CountryId!.Value;
                        currentAddress.GovernateId = dto.GovernateId!.Value;
                        currentAddress.AddressLine1 = dto.AddressLine!;
                    }
                }
                else
                {
                    var newAddress = new Address
                    {
                        CountryId = dto.CountryId!.Value,
                        GovernateId = dto.GovernateId!.Value,
                        AddressLine1 = dto.AddressLine!
                    };
                    _context.TbAddresses.Add(newAddress);
                    interviewData.Interview.Address = newAddress;
                }
            }
            else
            {
                // Phone interview
                interviewData.Interview.MeetingLink = dto.MeetingLink;
            }

            if (!string.IsNullOrEmpty(interviewData.Interview.ExpirationHangfireJobId))
            {
                BackgroundJob.Delete(interviewData.Interview.ExpirationHangfireJobId);
            }

            var delay = utcInterviewDate - DateTime.UtcNow;
            var newHangfireJobId = BackgroundJob.Schedule<InterviewExpirationService>(
                service => service.ExpireInterview(interviewData.Interview.Id),
                delay);

            interviewData.Interview.ExpirationHangfireJobId = newHangfireJobId;

            await _context.SaveChangesAsync();

            // Send email to candidate
            if (!string.IsNullOrWhiteSpace(interviewData.CandidateEmail))
            {
                try
                {
                    string candidateName = interviewData.CandidateName.Trim();
                    if (string.IsNullOrWhiteSpace(candidateName))
                    {
                        candidateName = "Candidate";
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
                        //var country = await _context.TbCountries.FindAsync(dto.CountryId!.Value);
                        //var governate = await _context.TbGovernates.FindAsync(dto.GovernateId!.Value);
                        locationDetails = $"{dto.AddressLine}, {governateName}, {countryName}";
                    }
                    else if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
                    {
                        locationDetails = dto.MeetingLink ?? string.Empty;
                    }

                    string formattedDate = $"{utcInterviewDate:yyyy-MM-dd HH:mm} UTC";

                    string emailContent = BuildInterviewEmailBody(
                        candidateName,
                        interviewData.JobTitle,
                        interviewData.EmployerName,
                        interviewType,
                        formattedDate,
                        locationDetails,
                        dto.InterviewTypeId,
                        dto.Notes);

                    await _emailService.SendEmailAsync(
                        interviewData.CandidateEmail,
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
        }
    }
}
