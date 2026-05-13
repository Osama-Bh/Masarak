using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.CompanyInterviewDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;
using GoWork.Enums;
using GoWork.Models;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.InterviewService
{
    public class InterviewService : IInterviewService
    {
        private readonly ApplicationDbContext _context;

        public InterviewService(ApplicationDbContext context)
        {
            _context = context;
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
            if (!string.Equals(action, "Accept", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid action. Use 'Accept' or 'Cancel'.");
            }

            // Find interview and verify ownership through Application → Seeker
            var interview = await _context.TbInterviews
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.SeekerId == seeker.Id);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            // Enforce: only Scheduled interviews can be acted upon
            if (interview.InterviewStatusId != (int)Enums.InterviewStatusEnum.Scheduled)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview can only be accepted or cancelled when in Scheduled status.");

            // Map action to status
            interview.InterviewStatusId = string.Equals(action, "Accept", StringComparison.OrdinalIgnoreCase)
                ? (int)Enums.InterviewStatusEnum.Confirmed
                : (int)Enums.InterviewStatusEnum.Cancelled;

            interview.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var message = string.Equals(action, "Accept", StringComparison.OrdinalIgnoreCase)
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

        public async Task<ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>> GetCompanyInterviewsAsync(
            int employerId, CompanyInterviewsRequestDTO request)
        {
            // Base query: all interviews for this employer's jobs
            var baseQuery = _context.TbInterviews
                .Where(i => i.Application.Job.EmployerId == employerId);

            // Search filter: by candidate name or job title
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                baseQuery = baseQuery.Where(i =>
                    i.Application.Job.Title.ToLower().Contains(search) ||
                    (i.Application.Seeker.FirsName + " " + i.Application.Seeker.MiddleName + " " + i.Application.Seeker.LastName)
                        .ToLower().Contains(search)
                );
            }

            // Filter by interview status
            if (request.InterviewStatusId.HasValue)
            {
                baseQuery = baseQuery.Where(i => i.InterviewStatusId == request.InterviewStatusId.Value);
            }

            // Filter by job
            if (request.JobId.HasValue)
            {
                baseQuery = baseQuery.Where(i => i.Application.JobId == request.JobId.Value);
            }

            // Sort by newest first
            baseQuery = baseQuery.OrderByDescending(i => i.InterviewDate);

            // Count before pagination
            var totalCount = await baseQuery.CountAsync();

            var today = DateTime.UtcNow.Date;

            // Paginate and project
            var items = await baseQuery
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(i => new CompanyInterviewListItemDTO
                {
                    InterviewId = i.Id,
                    ApplicationId = i.ApplicationId,
                    CandidateName = i.Application.Seeker.FirsName + " " + i.Application.Seeker.MiddleName + " " + i.Application.Seeker.LastName,
                    JobTitle = i.Application.Job.Title,
                    InterviewDate = i.InterviewDate,
                    InterviewType = i.InterviewType.Name,
                    InterviewStatus = i.InterviewStatus.Name,
                    // Location: InPerson → address string, Online → meeting link
                    Location = i.InterviewTypeId == (int)InterviewTypeEnum.InPerson
                        ? (i.Address != null
                            ? i.Address.Country.Name + ", " + i.Address.Governate.Name + ", " + i.Address.AddressLine1
                            : null)
                        : i.MeetingLink,

                    // Action flags
                    // Scheduled → cancel + reschedule
                    CanCancel = i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled
                             || (i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                                 && i.InterviewDate.Date != today),
                    CanReschedule = i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled,
                    // Confirmed + today → complete + missing
                    CanComplete = i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                                && i.InterviewDate.Date == today,
                    CanMarkMissing = i.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                                   && i.InterviewDate.Date == today
                })
                .ToListAsync();

            return new ApiResponse<PaginatedResult<CompanyInterviewListItemDTO>>(200,
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
            var statuses = await _context.TbInterviewStatuses
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
            var isConfirmedNotToday = interview.InterviewStatusId == (int)InterviewStatusEnum.Confirmed
                                   && interview.InterviewDate.Date != DateTime.UtcNow.Date;

            if (!isScheduled && !isConfirmedNotToday)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Interview can only be cancelled when in Scheduled status, or Confirmed status with a future date.");

            // Cancel the interview
            interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;

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
            if (dto.InterviewDate <= DateTime.UtcNow)
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
            interview.InterviewDate = dto.InterviewDate;
            interview.InterviewTypeId = dto.InterviewTypeId;
            interview.Notes = dto.Notes;
            interview.RespondedAt = null; // Reset candidate response

            if (dto.InterviewTypeId == (int)InterviewTypeEnum.Online)
            {
                interview.MeetingLink = dto.MeetingLink;
            }
            else if (dto.InterviewTypeId == (int)InterviewTypeEnum.InPerson)
            {
                interview.MeetingLink = null;

                // Create new address for the rescheduled interview
                var address = new Address
                {
                    CountryId = dto.CountryId!.Value,
                    GovernateId = dto.GovernateId!.Value,
                    AddressLine1 = dto.AddressLine!
                };
                _context.TbAddresses.Add(address);
                await _context.SaveChangesAsync();
                interview.AddressId = address.Id;
            }
            else
            {
                // Phone interview
                interview.MeetingLink = dto.MeetingLink;
            }

            // Keep status as Scheduled (candidate must re-confirm)
            interview.InterviewStatusId = (int)InterviewStatusEnum.Scheduled;

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

            if (interview.InterviewDate.Date != DateTime.UtcNow.Date)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Interview can only be marked as completed on the interview date.");

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

            if (interview.InterviewDate.Date != DateTime.UtcNow.Date)
                return new ApiResponse<ConfirmationResponseDTO>(400,
                    "Interview can only be marked as missing on the interview date.");

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
