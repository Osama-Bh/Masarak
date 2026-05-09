using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.ApplicationDTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Services.FileService;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.ApplicationService
{
    public class ApplicationService : IApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;

        public ApplicationService(ApplicationDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
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

            var application = await _context.TbApplications
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.SeekerId == seeker.Id);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Only applications under pending review can be withdrawn.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Withdrawn;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Application withdrawn successfully." });
        }


        // ----------------------

        //public async Task<PaginatedResult<CompanyApplicationDTO>> GetJobApplicationsAsync(int employerUserId, CompanyApplicationsFilterDTO filter)
        //{
        //    var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
        //    if (employer == null)
        //    {
        //        throw new Exception("Employer not found.");
        //    }

        //    var query = _context.TbApplications
        //        .Include(a => a.Seeker)
        //        .ThenInclude(s => s.ApplicationUser)
        //        .Include(a => a.Job)
        //        .Include(a => a.ApplicationStatus)
        //        .Where(a => a.Job.EmployerId == employer.Id)
        //        .AsQueryable();

        //    if (filter.JobId.HasValue && filter.JobId.Value > 0)
        //    {
        //        query = query.Where(a => a.JobId == filter.JobId.Value);
        //    }

        //    if (filter.StatusId.HasValue && filter.StatusId.Value > 0)
        //    {
        //        query = query.Where(a => a.ApplicationStatusId == filter.StatusId.Value);
        //    }

        //    if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        //    {
        //        var searchTerm = filter.SearchTerm.ToLower();
        //        query = query.Where(a =>
        //            (a.Seeker.FirsName + " " + a.Seeker.LastName).ToLower().Contains(searchTerm) ||
        //            (a.Seeker.ApplicationUser.Email != null && a.Seeker.ApplicationUser.Email.ToLower().Contains(searchTerm))
        //        );
        //    }

        //    var totalCount = await query.CountAsync();

        //    var applications = await query
        //        .OrderByDescending(a => a.ApplicationDate)
        //        .Skip((filter.Page - 1) * filter.PageSize)
        //        .Take(filter.PageSize)
        //        .Select(a => new CompanyApplicationDTO
        //        {
        //            ApplicationId = a.Id,
        //            CandidateName = $"{a.Seeker.FirsName} {a.Seeker.LastName}",
        //            CandidateEmail = a.Seeker.ApplicationUser.Email ?? string.Empty,
        //            JobTitle = a.Job.Title,
        //            ApplicationDate = a.ApplicationDate,
        //            CandidateDescription = a.Seeker.Major ?? string.Empty,
        //            ResumeUrl = a.Seeker.ResumeUrl,
        //            StatusId = a.ApplicationStatusId,
        //            StatusName = a.ApplicationStatus.Name
        //        })
        //        .ToListAsync();

        //    return new PaginatedResult<CompanyApplicationDTO>
        //    {
        //        Items = applications,
        //        TotalCount = totalCount,
        //        CurrentPage = filter.Page,
        //        PageSize = filter.PageSize
        //    };
        //}


        public async Task<PaginatedResult<CompanyApplicationDTO>> GetJobApplicationsAsync(
   int employerUserId,
   CompanyApplicationsFilterDTO filter)
        {
            var employer = await _context.TbEmployers
                .FirstOrDefaultAsync(e => e.UserId == employerUserId);

            if (employer == null)
                throw new Exception("Employer not found.");

            var query = _context.TbApplications
                .Include(a => a.Seeker)
                    .ThenInclude(s => s.ApplicationUser)
                .Include(a => a.Job)
                .Include(a => a.ApplicationStatus)
                .Where(a => a.Job.EmployerId == employer.Id)
                .AsQueryable();

            if (filter.JobId.HasValue && filter.JobId.Value > 0)
            {
                query = query.Where(a => a.JobId == filter.JobId.Value);
            }

            if (filter.StatusId.HasValue && filter.StatusId.Value > 0)
            {
                query = query.Where(a => a.ApplicationStatusId == filter.StatusId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.ToLower();

                query = query.Where(a =>
                    (a.Seeker.FirsName + " " + a.Seeker.LastName)
                        .ToLower()
                        .Contains(searchTerm)
                    ||
                    (a.Seeker.ApplicationUser.Email != null &&
                     a.Seeker.ApplicationUser.Email.ToLower().Contains(searchTerm))
                );
            }

            var totalCount = await query.CountAsync();

            // STEP 1: Fetch raw data first
            var rawApplications = await query
                .OrderByDescending(a => a.ApplicationDate)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new
                {
                    a.Id,
                    CandidateName = a.Seeker.FirsName + " " + a.Seeker.LastName,
                    CandidateEmail = a.Seeker.ApplicationUser.Email,
                    JobTitle = a.Job.Title,
                    a.ApplicationDate,
                    CandidateDescription = a.Seeker.Major,
                    ResumeBlobUrl = a.Seeker.ResumeUrl,
                    StatusId = a.ApplicationStatusId,
                    StatusName = a.ApplicationStatus.Name
                })
                .ToListAsync();

            // STEP 2: Transform with FileService in memory
            var applications = rawApplications
                .Select(a => new CompanyApplicationDTO
                {
                    ApplicationId = a.Id,
                    CandidateName = a.CandidateName,
                    CandidateEmail = a.CandidateEmail ?? string.Empty,
                    JobTitle = a.JobTitle,
                    ApplicationDate = a.ApplicationDate,
                    CandidateDescription = a.CandidateDescription ?? string.Empty,
                    ResumeUrl = string.IsNullOrWhiteSpace(a.ResumeBlobUrl)
                        ? null
                        : _fileService.DownloadUrlAsync(a.ResumeBlobUrl).SasUrl,
                    StatusId = a.StatusId,
                    StatusName = a.StatusName,
                    CanAction = a.StatusId == (int)ApplicationStatusEnum.PendingReview
                })
                .ToList();

            return new PaginatedResult<CompanyApplicationDTO>
            {
                Items = applications,
                TotalCount = totalCount,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<bool> UpdateApplicationStatusAsync(int employerUserId, int applicationId, int newStatusId)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null)
            {
                return false;
            }

            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employer.Id);

            if (application == null)
            {
                return false;
            }

            var statusExists = await _context.TbApplicationStatuses.AnyAsync(s => s.Id == newStatusId);
            if (!statusExists)
            {
                return false;
            }

            application.ApplicationStatusId = newStatusId;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> ShortlistApplicationAsync(int employerUserId, int applicationId)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employer.Id);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Only applications in Pending Review status can be shortlisted.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Shortlisted;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Application shortlisted successfully." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> RejectApplicationAsync(int employerUserId, int applicationId)
        {
            var employer = await _context.TbEmployers.FirstOrDefaultAsync(e => e.UserId == employerUserId);
            if (employer == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Employer not found.");

            var application = await _context.TbApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.EmployerId == employer.Id);

            if (application == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Application not found.");

            if (application.ApplicationStatusId != (int)ApplicationStatusEnum.PendingReview)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Only applications in Pending Review status can be rejected from this page.");

            application.ApplicationStatusId = (int)ApplicationStatusEnum.Rejected;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Application rejected successfully." });
        }
    }
}
