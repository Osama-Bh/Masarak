
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using GoWork.Authorization.Handlers;
using GoWork.Authorization.Requirements;
using GoWork.Data;
using GoWork.Service.AccountService;
using GoWork.Services.AdminService;
using GoWork.Services.ApplicationService;
using GoWork.Services.CurrentUserService;
using GoWork.Services.EmailService;
using GoWork.Services.FeedbackService;
using GoWork.Services.FileService;
using GoWork.Services.FirebaseNotificationSender;
using GoWork.Services.InterviewService;
using GoWork.Services.JobService;
using GoWork.Services.NotificationService;
using Hangfire;
using GoWork.Infrastructure.Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace GoWork
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Initialize Firebase
            var firebaseConfigPath = builder.Configuration["Firebase:CredentialsPath"];
            if (!string.IsNullOrEmpty(firebaseConfigPath) && System.IO.File.Exists(firebaseConfigPath))
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(firebaseConfigPath)
                });
            }

            // Add services to the container.

            builder.Services.AddControllers().
                AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Configure EF Core with SQL Server
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            #region ASP.NET Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true; // email must be unique for each user

                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
            #endregion

            // ✅ ADDITION: Token lifetime for email confirmation & reset password
            builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            {
                options.TokenLifespan = TimeSpan.FromMinutes(15);
            });

            #region Swagger
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            #endregion

            //Hangfire configuration
            builder.Services.AddHangfire(config =>
            config.UseSqlServerStorage(
                builder.Configuration.GetConnectionString("DefaultConnection")));

            if (!builder.Environment.IsDevelopment())
            {
                builder.Services.AddHangfireServer();
            }

            // ================================
            // Cookie settings (VERY IMPORTANT)
            // ================================
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None; // REQUIRED for cross-site
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
            });


            #region JWT Authantication
            // we can also call this from another class like extension class or write it here 
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JWT:Issuer"],
                    ValidAudience = builder.Configuration["JWT:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"])),
                    RoleClaimType = ClaimTypes.Role  // ← This line is important
                };
                //Added

                // ✅ ADDITION: Read JWT from HttpOnly cookie
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["access_token"];
                        return Task.CompletedTask;
                    }
                };
            });


            #endregion

            #region Dependency Injection
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IFileService, FileService>();
            builder.Services.AddScoped<IAdminService, AdminService>();
            builder.Services.AddScoped<GoWork.Services.JobService.IJobService, GoWork.Services.JobService.JobService>();
            builder.Services.AddScoped<IApplicationService, ApplicationService>();
            builder.Services.AddScoped<IInterviewService, InterviewService>();
            builder.Services.AddScoped<IFeedbackService, FeedbackService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IFirebaseNotificationSender, FirebaseNotificationSender>();
            builder.Services.AddScoped<IAuthorizationHandler, ApplicationAuthorizationHandler>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IAuthorizationHandler, InterviewAuthorizationHandler>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<JobExpirationService>();
            builder.Services.AddScoped<JobMaintenanceService>();
            #endregion

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("IsCandidateOwnInterviewPolicy", policy => policy.Requirements.Add(new CandidateOwnInterviewRquirements()));
            });

            #region Cors Settings
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("GoWorkApiCorePolicy", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:3000",
                            "https://go-work-next-js.vercel.app",
                            "https://masarak.app"
                        ).AllowAnyHeader().AllowAnyMethod().AllowCredentials(); 

                });
            }
            );
            #endregion

            //builder.Services.AddRateLimiter(options =>
            //{
            //    options.AddPolicy("OtpPolicy", context =>
            //    {
            //        var email = context.Request.Query["email"].ToString();

            //        if (string.IsNullOrEmpty(email))
            //            email = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            //        return RateLimitPartition.GetFixedWindowLimiter(
            //            partitionKey: email,
            //            factory: _ => new FixedWindowRateLimiterOptions
            //            {
            //                PermitLimit = 3,
            //                Window = TimeSpan.FromMinutes(5),
            //                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            //                QueueLimit = 0
            //            });
            //    });
            //});

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            //app.UseStaticFiles();       // Enables serving static files from wwwroot (e.g., CSS, JS, images)

            app.UseCors("GoWorkApiCorePolicy");

            app.UseAuthentication();

            app.UseAuthorization();

            if (app.Environment.IsDevelopment())
            {
                app.UseHangfireDashboard();
            }
            else
            {
                app.UseHangfireDashboard("/hangfire", new DashboardOptions
                {
                    Authorization = new[] { new HangfireAuthorizationFilter() }
                });
            }

            // Hangfire recurring jobs
            //if (!app.Environment.IsDevelopment())
            //{
                RecurringJob.AddOrUpdate<JobMaintenanceService>(
                    "expire-missed-jobs",
                    service => service.ExpireMissedJobs(),
                    Cron.Hourly);
            //}

            //app.UseRateLimiter();

            app.Run();
        }
    }
}