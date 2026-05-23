using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs;
using GoWork.DTOs.DashboardDTOs;
using GoWork.DTOs.FeedbackDTOs;
using GoWork.Enums;
using GoWork.Models;
using GoWork.Services.EmailService;
using GoWork.Services.FileService;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.FeedbackService
{
    public class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly IEmailService _emailService;
        public FeedbackService(ApplicationDbContext context, IFileService fileService,IEmailService emailService)
        {
            _context = context;
            _fileService = fileService;
            _emailService = emailService;
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

        public async Task<ApiResponse<FeedbackStatisticsDTO>> GetFeedbackStatisticsAsync()
        {
            var feedbacks = _context.TbFeedbacks.AsQueryable();

            var stats = new FeedbackStatisticsDTO
            {
                ComplaintsCount = await feedbacks.CountAsync(f => f.FeedbackTypeId == (int)FeedbackTypeEnum.Complaint),
                FeatureRequestsCount = await feedbacks.CountAsync(f => f.FeedbackTypeId == (int)FeedbackTypeEnum.FeatureRequest),
                ReadFeedbacksCount = await feedbacks.CountAsync(f => f.IsRead),
                UnreadFeedbacksCount = await feedbacks.CountAsync(f => !f.IsRead)
            };

            return new ApiResponse<FeedbackStatisticsDTO>(200, stats);
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

        private string PrepareEmailBody(string Message)
        {
            return $@"                <!DOCTYPE html>
                <html lang=""ar"" dir=""rtl"">
                <head>
                    <meta charset=""UTF-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <title>Masarak Email</title>
                </head>
                <body style=""margin:0; background:#f5f5f5;"">

                    <div style=""font-family: Tahoma, Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e0e0e0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.05); background:#ffffff;"">

                        <!-- Header -->
                        <div style=""background: linear-gradient(135deg, #01bafd 0%, #0199db 100%); padding: 30px; text-align: center;"">
                            <h1 style=""color: white; margin: 0; font-size: 28px; font-weight: 700;"">
                                Masarak.
                            </h1>
                        </div>

                        <!-- Content -->
                        <div style=""padding: 30px; text-align: right;"">

                            <h2 style=""color: #1f2937; margin-top: 0; font-size: 22px; font-weight: 600;"">
                                مرحباً
                            </h2>

                            <p style=""color: #4b5563; line-height: 1.8; font-size: 16px;"">
                                شكراً لتواصلك معنا. قمنا بمراجعة ملاحظتك بعناية، ويسعدنا أن نقدم لك الرد التالي
                            </p>

                            <!-- Message Box -->
                            <div style=""background-color: #f9fafb; border-right: 4px solid #02b5f1;border-left: 4px solid #02b5f1; padding: 15px; margin: 30px 0; border-radius: 8px; box-shadow: inset 0 2px 4px rgba(0,0,0,0.02);"">
                                <p style=""color: #374151; font-size: 14px; font-weight: bold; margin: 0; line-height: 1.8; white-space: pre-wrap;"">{Message}</p>
                            </div>

                            <p style=""color: #4b5563; line-height: 1.8; font-size: 16px;"">
                                نحن نقدر ملاحظاتك ونسعى دائماً إلى تحسين خدماتنا بناءً على اقتراحاتك. إذا كانت لديك أي استفسارات إضافية أو تحتاج إلى مساعدة أخرى، فلا تتردد في التواصل معنا
                            </p>

                            <!-- Signature -->
                            <div style=""margin-top: 40px; padding-top: 25px; border-top: 1px solid #f3f4f6; text-align: center;"">
                                <p style=""color: #9ca3af; font-size: 14px; margin: 0;"">
                                    مع أطيب التحيات
                                </p>

                                <p style=""color: #02b5f1; font-weight: 700; font-size: 18px; margin: 8px 0;"">
                                    Masarak.
                                </p>
                            </div>

                        </div>

                        <!-- Footer -->
                        <div style=""background-color: #f9fafb; padding: 25px; text-align: center; border-top: 1px solid #f3f4f6;"">
            
                            <p style=""color: #9ca3af; font-size: 13px; margin: 0;"">
                                © 2026 Masarak. نبني مستقبل العمل
                            </p>

     

                        </div>

                    </div>

                </body>
                </html>";

            
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> SendEmailReplyAsync(SendEmailRequestDTO dto)
        {
            try
            {
                var defaultSubject = "Response to your Feedback - GoWork";

                var htmlBody = PrepareEmailBody(dto.Message);

                await _emailService.SendEmailAsync(dto.Email, defaultSubject, htmlBody);

                return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO { Message = "Email sent successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
                return new ApiResponse<ConfirmationResponseDTO>(500, new ConfirmationResponseDTO { Message = "Failed to send email." });
            }
        }
    }

}
