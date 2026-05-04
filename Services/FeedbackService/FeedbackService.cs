using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.FeedbackDTOs;
using GoWork.Models;
using GoWork.Services.FileService;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.FeedbackService
{
    public class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        public FeedbackService(ApplicationDbContext context, IFileService fileService)
        {
            _context = context;
            _fileService = fileService;
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> SubmitFeedbackAsync(int userId, SubmitFeedbackDTO dto)
        {
            // Validate that the requested feedback type exists in TbFeedbackTypes
            var feedbackTypeId = (int)dto.FeedbackType;

            var typeExists = await _context.TbFeedbackTypes
                .AnyAsync(ft => ft.Id == feedbackTypeId);

            if (!typeExists)
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid feedback type.");

            var feedback = new Feedback
            {
                ReviewerId     = userId,
                FeedbackTypeId = feedbackTypeId,
                Message        = dto.Message,
                IsRead         = false,
                CreatedAt      = DateTime.Now
            };

            await _context.TbFeedbacks.AddAsync(feedback);
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Feedback submitted successfully." });
        }

        //public async Task<ApiResponse<PaginatedResult<FeedbackResponseDTO>>> GetAllFeedbacksAsync(int? feedbackTypeId = null, int pageNumber = 1, int pageSize = 10)
        //{
        //    var query = _context.TbFeedbacks
        //        .Include(f => f.AppUser).ThenInclude(u => u.Seeker)
        //        .Include(f => f.AppUser).ThenInclude(u => u.Employer)
        //        .Include(f => f.FeedbackType)
        //        .AsQueryable();

        //    if (feedbackTypeId.HasValue)
        //    {
        //        query = query.Where(f => f.FeedbackTypeId == feedbackTypeId.Value);
        //    }

        //    var totalCount = await query.CountAsync();

        //    var feedbacks = await query
        //        .OrderByDescending(f => f.CreatedAt)
        //        .Skip((pageNumber - 1) * pageSize)
        //        .Take(pageSize)
        //        .Select(f => new FeedbackResponseDTO
        //        {
        //            Id = f.Id,
        //            //ReviewerName = f.AppUser.Name ??
        //            //              (f.AppUser.Seeker != null ? f.AppUser.Seeker.FirsName + " " + f.AppUser.Seeker.LastName :
        //            //              (f.AppUser.Employer != null ? f.AppUser.Employer.ComapnyName :
        //            //              f.AppUser.Email ?? "Unknown")),
        //            ReviewerName = f.AppUser.Name ??
        //                          (_context.TbSeekers.Where(s => s.UserId == f.ReviewerId).Select(s => s.FirsName + " " + s.LastName).FirstOrDefault() ??
        //                          (_context.TbEmployers.Where(e => e.UserId == f.ReviewerId).Select(e => e.ComapnyName).FirstOrDefault() ??
        //                          f.AppUser.Email ?? "Unknown")),
        //            ReviewerEmail = f.AppUser.Email ?? "No Email",
        //            LogoUrl = _context.TbEmployers.Where(e => e.UserId == f.ReviewerId).Select(e => e.LogoUrl).FirstOrDefault() ??
        //                      _context.TbSeekers.Where(s => s.UserId == f.ReviewerId).Select(s => s.ProfilePhoto).FirstOrDefault(),
        //            FeedbackTypeName = f.FeedbackType.Name,
        //            Message = f.Message,
        //            IsRead = f.IsRead,
        //            CreatedAt = f.CreatedAt
        //        })
        //        .ToListAsync();

        //    // Transform Blob URIs to SAS URLs using IFileService
        //    foreach (var fb in feedbacks)
        //    {
        //        if (!string.IsNullOrEmpty(fb.LogoUrl))
        //        {
        //            var sasResult = _fileService.DownloadUrlAsync(fb.LogoUrl);
        //            if (sasResult.Succeeded)
        //            {
        //                fb.LogoUrl = sasResult.SasUrl;
        //            }
        //        }
        //    }

        //    var result = new PaginatedResult<FeedbackResponseDTO>
        //    {
        //        Items = feedbacks,
        //        CurrentPage = pageNumber,
        //        PageSize = pageSize,
        //        TotalCount = totalCount
        //    };

        //    return new ApiResponse<PaginatedResult<FeedbackResponseDTO>>(200, result);
        //}

        public async Task<ApiResponse<PaginatedResult<FeedbackResponseDTO>>> GetAllFeedbacksAsync(int? feedbackTypeId = null, bool? isRead = null, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.TbFeedbacks
                .Include(f => f.AppUser)
                .Include(f => f.FeedbackType)
                .AsQueryable();

            if (feedbackTypeId.HasValue)
            {
                query = query.Where(f => f.FeedbackTypeId == feedbackTypeId.Value);
            }

            if (isRead.HasValue)
            {
                query = query.Where(f => f.IsRead == isRead.Value);
            }

            var totalCount = await query.CountAsync();

            var feedbacks = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new FeedbackResponseDTO
                {
                    Id = f.Id,
                    ReviewerName = f.AppUser.Name ??
                                  (_context.TbSeekers.Where(s => s.UserId == f.ReviewerId).Select(s => s.FirsName + " " + s.LastName).FirstOrDefault() ??
                                  (_context.TbEmployers.Where(e => e.UserId == f.ReviewerId).Select(e => e.ComapnyName).FirstOrDefault() ??
                                  f.AppUser.Email ?? "Unknown")),
                    ReviewerEmail = f.AppUser.Email ?? "No Email",
                    ReviewerType = _context.TbEmployers.Any(e => e.UserId == f.ReviewerId) ? "Company" :
                                   (_context.TbSeekers.Any(s => s.UserId == f.ReviewerId) ? "Candidate" : "User"),
                    LogoUrl = _context.TbEmployers.Where(e => e.UserId == f.ReviewerId).Select(e => e.LogoUrl).FirstOrDefault() ??
                              _context.TbSeekers.Where(s => s.UserId == f.ReviewerId).Select(s => s.ProfilePhoto).FirstOrDefault(),
                    FeedbackTypeName = f.FeedbackType.Name,
                    Message = f.Message,
                    IsRead = f.IsRead,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            // Transform Blob URIs to SAS URLs using IFileService
            foreach (var fb in feedbacks)
            {
                if (!string.IsNullOrEmpty(fb.LogoUrl))
                {
                    var sasResult = _fileService.DownloadUrlAsync(fb.LogoUrl);
                    if (sasResult.Succeeded)
                    {
                        fb.LogoUrl = sasResult.SasUrl;
                    }
                }
            }

            var result = new PaginatedResult<FeedbackResponseDTO>
            {
                Items = feedbacks,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return new ApiResponse<PaginatedResult<FeedbackResponseDTO>>(200, result);
        }

        public async Task<ApiResponse<List<LookUpDTO>>> GetFeedbackTypesAsync()
        {
            var types = await _context.TbFeedbackTypes
                .OrderBy(t => t.SortOrder)
                .Select(t => new LookUpDTO
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToListAsync();

            return new ApiResponse<List<LookUpDTO>>(200, types);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> MarkAsReadAsync(int feedbackId)
        {
            var feedback = await _context.TbFeedbacks.FindAsync(feedbackId);
            if (feedback == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Feedback not found.");

            feedback.IsRead = true;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Feedback marked as read." });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> DeleteFeedbackAsync(int feedbackId)
        {
            var feedback = await _context.TbFeedbacks.FindAsync(feedbackId);
            if (feedback == null)
                return new ApiResponse<ConfirmationResponseDTO>(404, "Feedback not found.");

            _context.TbFeedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200,
                new ConfirmationResponseDTO { Message = "Feedback deleted successfully." });
        }
    }
}
