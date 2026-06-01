using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.DashboardDTOs;
using GoWork.Enums;
using GoWork.Services.EmailService;
using GoWork.Services.FileService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Services.AdminService
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileService _fileService;
        private readonly IEmailService _emailService;

        public AdminService(ApplicationDbContext context, UserManager<ApplicationUser> userManager,IFileService fileService, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _fileService = fileService;
            _emailService = emailService;
        }

        private static readonly string[] WeeklyArabicLabels =
        {
            "السبت",
            "الأحد",
            "الاثنين",
            "الثلاثاء",
            "الأربعاء",
            "الخميس",
            "الجمعة"
        };

        private static DateTime GetStartOfWeek(DateTime utcNow)
        {
            var daysSinceSaturday = ((int)utcNow.DayOfWeek + 1) % 7;
            return utcNow.Date.AddDays(-daysSinceSaturday);
        }

        public async Task<ApiResponse<ChartPieResponseDTO>> GetAdminCompanyStatusDistributionChartAsync()
        {
            var items = new List<ChartPieItemDTO>
            {
                new()
                {
                    Key = "active",
                    Label = "موثقة",
                    Value = await _context.TbEmployers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.Active),
                    Fill = "var(--chart-1)"
                },
                new()
                {
                    Key = "pending",
                    Label = "قيد المراجعة",
                    Value = await _context.TbEmployers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.PendingApproval),
                    Fill = "var(--chart-2)"
                },
                new()
                {
                    Key = "rejected",
                    Label = "مرفوضة",
                    Value = await _context.TbEmployers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.Rejected),
                    Fill = "var(--chart-3)"
                },
                new()
                {
                    Key = "suspended",
                    Label = "موقوفة",
                    Value = await _context.TbEmployers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.Suspended),
                    Fill = "var(--chart-4)"
                }
            };

            return new ApiResponse<ChartPieResponseDTO>(200, new ChartPieResponseDTO
            {
                Title = "توزيع حالات الشركة",
                Description = "التوزيع الحالي",
                Items = items
            });
        }

        public async Task<ApiResponse<AdminCompanyRegistrationsChartResponseDTO>> GetAdminCompanyRegistrationsChartAsync()
        {
            var utcNow = DateTime.UtcNow;
            var startOfWeek = GetStartOfWeek(utcNow);
            var endOfWeek = startOfWeek.AddDays(7);

            var employers = await _context.TbEmployers
                .Where(e => e.CreatedAt >= startOfWeek && e.CreatedAt < endOfWeek)
                .Select(e => new { e.CreatedAt, e.EmployerStatusId })
                .ToListAsync();

            var items = Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var day = startOfWeek.AddDays(offset);
                    var nextDay = day.AddDays(1);
                    return new AdminCompanyRegistrationsChartPointDTO
                    {
                        Label = WeeklyArabicLabels[offset],
                        CompaniesRegistered = employers.Count(e => e.CreatedAt >= day && e.CreatedAt < nextDay),
                        PendingVerifications = employers.Count(e =>
                            e.CreatedAt >= day &&
                            e.CreatedAt < nextDay &&
                            e.EmployerStatusId == (int)EmployerStatusEnum.PendingApproval)
                    };
                })
                .ToList();

            return new ApiResponse<AdminCompanyRegistrationsChartResponseDTO>(200, new AdminCompanyRegistrationsChartResponseDTO
            {
                Title = "تسجيل الشركات",
                Description = "آخر 7 أيام",
                Period = "weekly",
                XKey = "label",
                Series = new List<ChartSeriesDTO>
                {
                    new() { Key = "companiesRegistered", Label = "الشركات المسجلة", Color = "var(--chart-1)" },
                    new() { Key = "pendingVerifications", Label = "طلبات التوثيق", Color = "var(--chart-2)" }
                },
                Items = items
            });
        }

        public async Task<ApiResponse<AdminDashboardStatisticsDTO>> GetAdminDashboardStatisticsAsync()
        {
            var utcNow = DateTime.UtcNow;
            var startOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1);

            var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
            var startOfWeek = utcNow.Date.AddDays(-daysSinceMonday);

            var stats = new AdminDashboardStatisticsDTO
            {
                TotalCompanies = await _context.TbEmployers.CountAsync(),
                TotalFeedbacks = await _context.TbFeedbacks.CountAsync(),
                PendingVerificationRequests = await _context.TbEmployers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.PendingApproval),
                UnreadFeedbacks = await _context.TbFeedbacks.CountAsync(f => !f.IsRead),
                TotalPublishedJobs = await _context.TbJobs.CountAsync(),
                CompaniesRegisteredThisMonth = await _context.TbEmployers.CountAsync(e => e.CreatedAt >= startOfMonth),
                NewFeedbacksThisWeek = await _context.TbFeedbacks.CountAsync(f => f.CreatedAt >= startOfWeek)
            };

            return new ApiResponse<AdminDashboardStatisticsDTO>(200, stats);
        }

        public async Task<ApiResponse<CompanyStatisticsDTO>> GetCompanyStatisticsAsync()
        {
            var employers = _context.TbEmployers;

            var stats = new CompanyStatisticsDTO
            {
                TotalCompanies = await employers.CountAsync(),
                PendingVerification = await employers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.PendingApproval),
                Verified = await employers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.Active),
                Rejected = await employers.CountAsync(e => e.EmployerStatusId == (int)EmployerStatusEnum.Rejected)
            };

            return new ApiResponse<CompanyStatisticsDTO>(200, stats);
        }

        public async Task<ApiResponse<PaginatedResult<CompanyListItemDTO>>> GetCompaniesAsync(
            int page, int pageSize, string? search, string? status, string? sortBy, string? sortOrder)
        {
            var query = _context.TbEmployers
                .Include(e => e.ApplicationUser)
                .Include(e => e.EmployerStatus)
                .AsQueryable();

            // Search by company name
            //if (!string.IsNullOrWhiteSpace(search))
            //{
            //    query = query.Where(e => e.ComapnyName.Contains(search));
            //}

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.ComapnyName.Contains(search)
                                      || e.ApplicationUser.Email!.Contains(search));
            }

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EmployerStatusEnum>(status, true, out var statusEnum))
            {
                query = query.Where(e => e.EmployerStatusId == (int)statusEnum);
            }

            // Sorting
            query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
            {
                ("name", "asc") => query.OrderBy(e => e.ComapnyName),
                ("name", _) => query.OrderByDescending(e => e.ComapnyName),
                ("email", "asc") => query.OrderBy(e => e.ApplicationUser.Email),
                ("email", _) => query.OrderByDescending(e => e.ApplicationUser.Email),
                ("status", "asc") => query.OrderBy(e => e.EmployerStatus.Name),
                ("status", _) => query.OrderByDescending(e => e.EmployerStatus.Name),
                (_, "asc") => query.OrderBy(e => e.CreatedAt),
                _ => query.OrderByDescending(e => e.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new CompanyListItemDTO
                {
                    Id = e.Id,
                    CompanyName = e.ComapnyName,
                    Industry = e.Industry,
                    Email = e.ApplicationUser.Email ?? string.Empty,
                    PhoneNumber = e.ApplicationUser.PhoneNumber ?? string.Empty,
                    CreatedAt = e.CreatedAt,
                    Status = e.EmployerStatus.Name,
                    LogoUrl = _fileService.DownloadUrlAsync(e.LogoUrl).SasUrl
                })
                .ToListAsync();

            var result = new PaginatedResult<CompanyListItemDTO>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return new ApiResponse<PaginatedResult<CompanyListItemDTO>>(200, result);
        }

        public async Task<ApiResponse<CompanyDetailDTO>> GetCompanyByIdAsync(int id)
        {
            var employer = await _context.TbEmployers
                .Include(e => e.ApplicationUser)
                .Include(e => e.EmployerStatus)
                .Include(e => e.Address)
                    .ThenInclude(a => a!.Governate)
                .Include(e => e.Jobs)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employer == null)
            {
                return new ApiResponse<CompanyDetailDTO>(404, "Company not found.");
            }
            var logoUrlResponse = _fileService
                    .DownloadUrlAsync(employer.LogoUrl);

            var detail = new CompanyDetailDTO
            {
                Id = employer.Id,
                CompanyName = employer.ComapnyName,
                Industry = employer.Industry,
                Email = employer.ApplicationUser.Email ?? string.Empty,
                PhoneNumber = employer.ApplicationUser.PhoneNumber ?? string.Empty,
                CreatedAt = employer.CreatedAt,
                Status = employer.EmployerStatus.Name,
                LogoUrl = logoUrlResponse.SasUrl,
                EmailConfirmed = employer.ApplicationUser.EmailConfirmed,
                TotalJobs = employer.Jobs?.Count ?? 0,
                City = employer.Address?.AddressLine1,
                Governate = employer.Address?.Governate?.Name
            };

            return new ApiResponse<CompanyDetailDTO>(200, detail);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateCompanyStatusAsync(int id, UpdateCompanyStatusDTO dto)
        {
            if (!Enum.TryParse<EmployerStatusEnum>(dto.Status, true, out var newStatus))
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid status value.");
            }

            //var employer = await _context.TbEmployers.FindAsync(id);

            var employer = await _context.TbEmployers
            .Include(e => e.ApplicationUser)
            .FirstOrDefaultAsync(e => e.Id == id);

            if (employer == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Company not found.");
            }

            // Prevent unnecessary update
            if (employer.EmployerStatusId == (int)newStatus)
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Status is already set to this value.");
            }

            employer.EmployerStatusId = (int)newStatus;
            await _context.SaveChangesAsync();

            // 🔔 Send Email Notification
            var userEmail = employer.ApplicationUser.Email;
            var companyName = employer.ComapnyName;

            var subject = "Company Status Updated";

            var content = $@"
              <div style=""padding: 30px; text-align: right"">
                <div style=""color: #4b5563; line-height: 1.8; font-size: 16px"">
                  <h1 style=""color: #1f2937; margin-top: 0; font-size: 18px; font-weight: 600;"" >
                    {companyName} مرحبًا
                  </h1>
                  <p style=""color: #555"">
                    .تم تحديث حالة شركتك 
                  </p>

                  <div
                    style=""
                      background-color: #f9fafb;
                      border-right: 4px solid #02b5f1;
                      border-left: 4px solid #02b5f1;
                      padding: 15px;
                      margin: 30px 0;
                      text-align: center;
                      border-radius: 8px;
                    ""
                  >
                    <p style=""margin: 0; font-weight: bold""> {newStatus} : الحالة الجديدة</p>
                  </div>

                  <p style=""color: #888; font-size: 14px"">
                   .شكراً لاستخدامك منصة مسارك 
                  </p>

                </div>";


            await _emailService.SendEmailAsync(
                userEmail,
                subject,
                content,
                companyName);


            //var body = $@"
            //<p>Dear {companyName},</p>
            //<p>Your company status has been updated.</p>
            //<p><strong>New Status:</strong> {newStatus}</p>
            //";

            //await _emailService.SendEmailAsync(userEmail, subject, body, companyName);

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Company status updated successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> DeleteCompanyAsync(int id)
        {
            var employer = await _context.TbEmployers.FindAsync(id);
            if (employer == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Company not found.");
            }

            // Soft delete: set status to Blocked
            employer.EmployerStatusId = (int)EmployerStatusEnum.Blocked;
            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Company has been blocked successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateCompanyAsync(int id, AdminUpdateCompanyDTO dto)
        {
            var employer = await _context.TbEmployers
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employer == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Company not found.");
            }

            // Update only the fields that are provided
            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
                employer.ComapnyName = dto.CompanyName;

            if (!string.IsNullOrWhiteSpace(dto.Industry))
                employer.Industry = dto.Industry;

            if (!string.IsNullOrWhiteSpace(dto.Email))
                employer.ApplicationUser.Email = dto.Email;

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                employer.ApplicationUser.PhoneNumber = dto.PhoneNumber;

            await _context.SaveChangesAsync();

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Company updated successfully."
            });
        }

        public async Task<ApiResponse<BulkActionResultDTO>> BulkActionAsync(BulkActionDTO dto)
        {
            if (dto.CompanyIds == null || dto.CompanyIds.Count == 0)
            {
                return new ApiResponse<BulkActionResultDTO>(400, "No companies selected.");
            }

            // Determine target status based on action
            int? targetStatusId = dto.Action?.ToLower() switch
            {
                "approve" => (int)EmployerStatusEnum.Active,
                "reject" => (int)EmployerStatusEnum.Rejected,
                //"suspend" or "delete" => (int)EmployerStatusEnum.Suspended,
                "suspend" => (int)EmployerStatusEnum.Suspended,
                "delete" => (int)EmployerStatusEnum.Blocked,
                _ => null
            };

            if (targetStatusId == null)
            {
                return new ApiResponse<BulkActionResultDTO>(400, "Invalid action. Allowed: Approve, Reject, Suspend, Delete.");
            }

            var employers = await _context.TbEmployers
                .Where(e => dto.CompanyIds.Contains(e.Id))
                .ToListAsync();

            int successCount = 0;
            int failedCount = 0;

            foreach (var employer in employers)
            {
                try
                {
                    employer.EmployerStatusId = targetStatusId.Value;
                    successCount++;
                }
                catch
                {
                    failedCount++;
                }
            }

            // Count IDs that were not found
            failedCount += dto.CompanyIds.Count - employers.Count;

            await _context.SaveChangesAsync();

            var result = new BulkActionResultDTO
            {
                SuccessCount = successCount,
                FailedCount = failedCount,
                Message = $"{successCount} companies processed successfully."
            };

            return new ApiResponse<BulkActionResultDTO>(200, result);
        }

        // ==================== Sub-Admin Management ====================

        private async Task<List<ApplicationUser>> GetSubAdminUsersQueryAsync()
        {
            var subAdminUsers = await _userManager.GetUsersInRoleAsync("SubAdmin");
            return subAdminUsers.ToList();
        }

        public async Task<ApiResponse<SubAdminStatisticsDTO>> GetSubAdminStatisticsAsync()
        {
            var subAdmins = await GetSubAdminUsersQueryAsync();

            var stats = new SubAdminStatisticsDTO
            {
                TotalSubAdmins = subAdmins.Count,
                ActiveSubAdmins = subAdmins.Count(u => u.Status == UserStatusEnum.Active.ToString()),
                SuspendedSubAdmins = subAdmins.Count(u => u.Status == UserStatusEnum.Suspended.ToString()),
                BlockedSubAdmins = subAdmins.Count(u=> u.Status== UserStatusEnum.Blocked.ToString()),
            };

            return new ApiResponse<SubAdminStatisticsDTO>(200, stats);
        }

        public async Task<ApiResponse<PaginatedResult<SubAdminListItemDTO>>> GetSubAdminsAsync(
            int page, int pageSize, string? search, string? status)
        {
            var subAdmins = await GetSubAdminUsersQueryAsync();

            // Search by name or email
            if (!string.IsNullOrWhiteSpace(search))
            {
                subAdmins = subAdmins
                    .Where(u => (u.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                             || (u.Email ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filter by status
            if (!string.IsNullOrWhiteSpace(status))
            {
                subAdmins = subAdmins.Where(u => u.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = subAdmins.Count;

            var items = subAdmins
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new SubAdminListItemDTO
                {
                    Id = u.Id,
                    Name = u.Name ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    PhoneNumber = u.PhoneNumber ?? string.Empty,
                    CreatedAt = u.CreatedAt,
                    Status = u.Status
                })
                .ToList();

            var result = new PaginatedResult<SubAdminListItemDTO>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return new ApiResponse<PaginatedResult<SubAdminListItemDTO>>(200, result);
        }

        public async Task<ApiResponse<SubAdminDetailDTO>> GetSubAdminByIdAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ApiResponse<SubAdminDetailDTO>(404, "Sub-admin not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("SubAdmin"))
            {
                return new ApiResponse<SubAdminDetailDTO>(404, "Sub-admin not found.");
            }

            var detail = new SubAdminDetailDTO
            {
                Id = user.Id,
                Name = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                CreatedAt = user.CreatedAt,
                Status = user.Status
            };

            return new ApiResponse<SubAdminDetailDTO>(200, detail);
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateSubAdminStatusAsync(int id, UpdateSubAdminStatusDTO dto)
        {
            if (!Enum.TryParse<UserStatusEnum>(dto.Status, true, out var newStatus))
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "Invalid status value. Allowed: Active, Suspended, Blocked.");
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("SubAdmin"))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            user.Status = newStatus.ToString();
            await _userManager.UpdateAsync(user);

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Sub-admin status updated successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> DeleteSubAdminAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("SubAdmin"))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            // Soft delete: set status to Blocked
            user.Status = UserStatusEnum.Blocked.ToString();
            await _userManager.UpdateAsync(user);

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Sub-admin has been blocked successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> UpdateSubAdminAsync(int id, UpdateSubAdminDTO dto)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("SubAdmin"))
            {
                return new ApiResponse<ConfirmationResponseDTO>(404, "Sub-admin not found.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
                user.UserName = dto.Name;

            if (!string.IsNullOrWhiteSpace(dto.Email))
                user.Email = dto.Email;

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber;

            await _userManager.UpdateAsync(user);

            return new ApiResponse<ConfirmationResponseDTO>(200, new ConfirmationResponseDTO
            {
                Message = "Sub-admin updated successfully."
            });
        }

        public async Task<ApiResponse<ConfirmationResponseDTO>> CreateSubAdminAsync(GoWork.DTOs.AuthDTOs.AdminRegistrationDTO dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                return new ApiResponse<ConfirmationResponseDTO>(400, "البريد الإلكتروني مستخدم بالفعل");
            }

            var user = new ApplicationUser
            {
                UserName = dto.Name,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                EmailConfirmed = true,
                Status = UserStatusEnum.Active.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return new ApiResponse<ConfirmationResponseDTO>(400, errors);
            }

            await _userManager.AddToRoleAsync(user, "SubAdmin");

            return new ApiResponse<ConfirmationResponseDTO>(201, new ConfirmationResponseDTO
            {
                Message = "Sub-admin created successfully."
            });
        }
    }
}
