using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.InterviewDTOs;
using GoWork.Enums;
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

        public async Task<ApiResponse<InterviewStatisticsDTO>> GetInterviewStatisticsAsync(int employerUserId)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                return new ApiResponse<InterviewStatisticsDTO>(404, "Employer not found.");

            var stats = await _context.TbInterviews
                .Where(i => i.Application.Job.EmployerId == employer.Id)
                .GroupBy(i => i.InterviewStatusId)
                .Select(g => new { StatusId = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = new InterviewStatisticsDTO
            {
                Scheduled   = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.Scheduled)?.Count ?? 0,
                Confirmed   = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.Confirmed)?.Count ?? 0,
                Completed   = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.Completed)?.Count ?? 0,
                Rescheduled = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.Rescheduled)?.Count ?? 0,
                Cancelled   = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.Cancelled)?.Count ?? 0,
                NoShow      = stats.FirstOrDefault(s => s.StatusId == (int)InterviewStatusEnum.NoShow)?.Count ?? 0
            };

            return new ApiResponse<InterviewStatisticsDTO>(200, result);
        }

        public async Task<ApiResponse<PaginatedResult<CompanyInterviewDTO>>> GetInterviewsAsync(
            int employerUserId, InterviewFilterDTO filter)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                return new ApiResponse<PaginatedResult<CompanyInterviewDTO>>(404, "Employer not found.");

            var query = _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Seeker).ThenInclude(s => s.ApplicationUser)
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .Include(i => i.InterviewStatus)
                .Include(i => i.InterviewType)
                .Include(i => i.Address).ThenInclude(a => a.Governate)
                .Where(i => i.Application.Job.EmployerId == employer.Id)
                .AsQueryable();

            if (filter.StatusId.HasValue && filter.StatusId.Value > 0)
                query = query.Where(i => i.InterviewStatusId == filter.StatusId.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(i =>
                    (i.Application.Seeker.FirsName + " " + i.Application.Seeker.LastName).ToLower().Contains(term) ||
                    i.Application.Job.Title.ToLower().Contains(term));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(i => i.InterviewDate)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(i => new CompanyInterviewDTO
                {
                    InterviewId       = i.Id,
                    ApplicationId     = i.ApplicationId,
                    CandidateName     = i.Application.Seeker.FirsName + " " + i.Application.Seeker.LastName,
                    CandidateEmail    = i.Application.Seeker.ApplicationUser.Email ?? string.Empty,
                    JobTitle          = i.Application.Job.Title,
                    InterviewDate     = i.InterviewDate,
                    InterviewTypeName = i.InterviewType.Name,
                    Location          = i.Address.AddressLine1 + " - " + i.Address.Governate.Name,
                    StatusId          = i.InterviewStatusId,
                    StatusName        = i.InterviewStatus.Name,
                    Notes             = i.Notes
                })
                .ToListAsync();

            return new ApiResponse<PaginatedResult<CompanyInterviewDTO>>(200, new PaginatedResult<CompanyInterviewDTO>
            {
                Items        = items,
                TotalCount   = totalCount,
                CurrentPage  = filter.Page,
                PageSize     = filter.PageSize
            });
        }

        public async Task<ApiResponse<CompanyInterviewDTO>> GetInterviewByIdAsync(
            int employerUserId, int interviewId)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                return new ApiResponse<CompanyInterviewDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Seeker).ThenInclude(s => s.ApplicationUser)
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .Include(i => i.InterviewStatus)
                .Include(i => i.InterviewType)
                .Include(i => i.Address).ThenInclude(a => a.Governate)
                .Where(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id)
                .Select(i => new CompanyInterviewDTO
                {
                    InterviewId       = i.Id,
                    ApplicationId     = i.ApplicationId,
                    CandidateName     = i.Application.Seeker.FirsName + " " + i.Application.Seeker.LastName,
                    CandidateEmail    = i.Application.Seeker.ApplicationUser.Email ?? string.Empty,
                    JobTitle          = i.Application.Job.Title,
                    InterviewDate     = i.InterviewDate,
                    InterviewTypeName = i.InterviewType.Name,
                    Location          = i.Address.AddressLine1 + " - " + i.Address.Governate.Name,
                    StatusId          = i.InterviewStatusId,
                    StatusName        = i.InterviewStatus.Name,
                    Notes             = i.Notes
                })
                .FirstOrDefaultAsync();

            if (interview == null)
                return new ApiResponse<CompanyInterviewDTO>(404, "Interview not found.");

            return new ApiResponse<CompanyInterviewDTO>(200, interview);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateInterviewStatusAsync(
            int employerUserId, int interviewId, UpdateInterviewStatusDTO dto)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            var statusExists = await _context.TbInterviewStatuses
                .AnyAsync(s => s.Id == dto.StatusId && s.IsActive);

            if (!statusExists)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid interview status.");

            interview.InterviewStatusId = dto.StatusId;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview status updated successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RescheduleInterviewAsync(
            int employerUserId, int interviewId, RescheduleInterviewDTO dto)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            interview.InterviewDate     = dto.NewDate;
            interview.InterviewStatusId = (int)InterviewStatusEnum.Rescheduled;

            if (!string.IsNullOrWhiteSpace(dto.Notes))
                interview.Notes = dto.Notes;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Interview rescheduled successfully." });
        }

        public async Task<ApiResponse<List<LookUpDTO>>> GetInterviewStatusesAsync()
        {
            var items = await _context.TbInterviewStatuses
                .Where(s => s.IsActive)
                .OrderBy(s => s.SortOrder)
                .Select(s => new LookUpDTO { Id = s.Id, Name = s.Name })
                .ToListAsync();

            return new ApiResponse<List<LookUpDTO>>(200, items);
        }

        public async Task<ApiResponse<PaginatedResult<CompanyApplicationDTO>>> GetShortlistedApplicationsAsync(int employerUserId, InterviewFilterDTO filter)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<PaginatedResult<CompanyApplicationDTO>>(404, "Employer not found.");

            var query = _context.TbApplications
                .Include(a => a.Seeker).ThenInclude(s => s.ApplicationUser)
                .Include(a => a.Job)
                .Include(a => a.ApplicationStatus)
                .Where(a => a.Job.EmployerId == employer.Id && a.ApplicationStatusId == (int)ApplicationStatusEnum.Shortlisted)
                // Optionally exclude those already scheduled
                .Where(a => !_context.TbInterviews.Any(i => i.ApplicationId == a.Id && i.InterviewStatusId == (int)InterviewStatusEnum.Scheduled))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(a =>
                    (a.Seeker.FirsName + " " + a.Seeker.LastName).ToLower().Contains(term) ||
                    a.Job.Title.ToLower().Contains(term));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.ApplicationDate)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new CompanyApplicationDTO
                {
                    ApplicationId = a.Id,
                    CandidateName = a.Seeker.FirsName + " " + a.Seeker.LastName,
                    CandidateEmail = a.Seeker.ApplicationUser.Email ?? string.Empty,
                    JobTitle = a.Job.Title,
                    ApplicationDate = a.ApplicationDate,
                    CandidateDescription = a.Seeker.Major ?? string.Empty,
                    StatusId = a.ApplicationStatusId,
                    StatusName = a.ApplicationStatus.Name,
                    CanAction = true // Shortlisted candidates are actionable in the interview context
                })
                .ToListAsync();

            return new ApiResponse<PaginatedResult<CompanyApplicationDTO>>(200, new PaginatedResult<CompanyApplicationDTO>
            {
                Items = items,
                TotalCount = totalCount,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> ScheduleInterviewAsync(int employerUserId, ScheduleInterviewDTO dto)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == dto.ApplicationId && a.Job.EmployerId == employer.Id);

            if (application == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            // Create Address
            var address = new Models.Address
            {
                AddressLine1 = dto.AddressLine1,
                CountryId = dto.CountryId,
                GovernateId = dto.GovernateId
            };
            _context.TbAddresses.Add(address);
            await _context.SaveChangesAsync(); // Save to get AddressId

            var interview = new Models.Interview
            {
                ApplicationId = dto.ApplicationId,
                InterviewDate = dto.InterviewDate,
                InterviewTypeId = dto.InterviewTypeId,
                Notes = dto.Notes,
                MeetingLink = dto.MeetingLink,
                AddressId = address.Id,
                InterviewStatusId = (int)InterviewStatusEnum.Scheduled
            };

            _context.TbInterviews.Add(interview);

            // Optionally update application status to 'Interview' stage if that's desired
            // application.ApplicationStatusId = (int)ApplicationStatusEnum.Interview;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Interview scheduled successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateInterviewAsync(int employerUserId, int interviewId, ScheduleInterviewDTO dto)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .Include(i => i.Address)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            interview.InterviewDate = dto.InterviewDate;
            interview.InterviewTypeId = dto.InterviewTypeId;
            interview.Notes = dto.Notes;
            interview.MeetingLink = dto.MeetingLink;
            interview.InterviewStatusId = (int)InterviewStatusEnum.Scheduled; // Maintain scheduled status (or set to Rescheduled)

            if (interview.Address != null)
            {
                interview.Address.AddressLine1 = dto.AddressLine1;
                interview.Address.CountryId = dto.CountryId;
                interview.Address.GovernateId = dto.GovernateId;
            }

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Interview updated successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkAsCancelledAsync(int employerUserId, int interviewId)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewDate.Date > DateTime.UtcNow.Date)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview can only be cancelled on or after the interview date.");

            interview.InterviewStatusId = (int)InterviewStatusEnum.Cancelled;
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.Rejected; // Requirement: update application status to rejected

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Interview cancelled and application rejected." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkAsMissingAsync(int employerUserId, int interviewId)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewDate.Date > DateTime.UtcNow.Date)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview can only be marked as missing on or after the interview date.");

            interview.InterviewStatusId = (int)InterviewStatusEnum.MissingInterview;
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.MissingInterview; // Requirement: update application status to missing interview

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Interview marked as missing." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> CompleteInterviewAsync(int employerUserId, int interviewId, InterviewOutcomeDTO dto)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var interview = await _context.TbInterviews
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .FirstOrDefaultAsync(i => i.Id == interviewId && i.Application.Job.EmployerId == employer.Id);

            if (interview == null) return new ApiResponse<ConfirmationResponseDTO>(404, "Interview not found.");

            if (interview.InterviewDate.Date > DateTime.UtcNow.Date)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Interview can only be completed on or after the interview date.");

            interview.InterviewStatusId = (int)InterviewStatusEnum.Completed;

            // Handle Application Outcome
            interview.Application.ApplicationStatusId = dto.IsHired
                ? (int)ApplicationStatusEnum.Hired
                : (int)ApplicationStatusEnum.Rejected;

            await _context.SaveChangesAsync();

            var resultMsg = dto.IsHired ? "Candidate hired successfully." : "Candidate rejected after interview.";
            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = resultMsg });
        }
    }
}
