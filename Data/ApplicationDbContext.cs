using GoWork.Enums;
using GoWork.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace GoWork.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        public virtual DbSet<Application> TbApplications { get; set; }
        public virtual DbSet<ApplicationStatus> TbApplicationStatuses { get; set; }
        public virtual DbSet<Category> TbCategories { get; set; }
        public virtual DbSet<Country> TbCountries { get; set; }
        public virtual DbSet<Currency> TbCurrencies { get; set; }
        public virtual DbSet<Employer> TbEmployers { get; set; }
        public virtual DbSet<EmployerStatus> TbEmployerStatuses { get; set; }
        public virtual DbSet<Feedback> TbFeedbacks { get; set; }
        public virtual DbSet<FeedbackType> TbFeedbackTypes { get; set; }
        public virtual DbSet<Governate> TbGovernates { get; set; }
        public virtual DbSet<Interview> TbInterviews { get; set; }
        public virtual DbSet<InterviewStatus> TbInterviewStatuses { get; set; }
        public virtual DbSet<InterviewType> TbInterviewTypes { get; set; }
        public virtual DbSet<Job> TbJobs { get; set; }
        public virtual DbSet<JobLocationType> TbJobLocationTypes { get; set; }
        public virtual DbSet<JobSkill> TbJobSkills { get; set; }
        public virtual DbSet<JobStatus> TbJobStatuses { get; set; }
        public virtual DbSet<JobType> TbJobTypes { get; set; }
        public virtual DbSet<Seeker> TbSeekers { get; set; }
        public virtual DbSet<SeekerSkill> TbSeekerSkills { get; set; }
        public virtual DbSet<Skill> TbSkills { get; set; }
        public virtual DbSet<Address> TbAddresses { get; set; }
        public virtual DbSet<Notification> TbNotifications { get; set; }
        public virtual DbSet<UserNotification> TbUserNotifications { get; set; }
        public virtual DbSet<DeviceToken> TbDeviceTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ---- Global safety net: Restrict all FKs by default ----
            // Runs first so every relationship not explicitly configured below
            // is protected from accidental cascade deletes.
            foreach (var foreignKey in builder.Model
                         .GetEntityTypes()
                         .SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // ---- IdentityUserRole → ApplicationUser (Cascade) ----
            // UserManager.DeleteAsync() removes the user row, so role assignments
            // in AspNetUserRoles must cascade-delete automatically.
            builder.Entity<IdentityUserRole<int>>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---- Job relationships ----
            builder.Entity<Job>()
                .HasOne(j => j.Employer)
                .WithMany(e => e.Jobs)
                .HasForeignKey(j => j.EmployerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Job>()
                .HasOne(j => j.Address)
                .WithOne()
                .HasForeignKey<Job>(j => j.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Job>()
                .HasIndex(j => j.AddressId)
                .IsUnique();

            // ---- Application relationships ----
            // Deleting a Job does NOT delete its applications (Restrict)
            builder.Entity<Application>()
                .HasOne(a => a.Job)
                .WithMany(j => j.Applications)
                .HasForeignKey(a => a.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting a Seeker cascades to their applications
            builder.Entity<Application>()
                .HasOne(a => a.Seeker)
                .WithMany(s => s.Applications)
                .HasForeignKey(a => a.SeekerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---- Interview relationships ----
            // Deleting an Application cascades to its interviews
            builder.Entity<Interview>()
                .HasOne(i => i.Application)
                .WithMany(a => a.Interviews)
                .HasForeignKey(i => i.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Interview>()
                .HasOne(i => i.InterviewType)
                .WithMany(it => it.Interviews)
                .HasForeignKey(i => i.InterviewTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Interview>()
                .HasOne(i => i.InterviewStatus)
                .WithMany(ins => ins.Interviews)
                .HasForeignKey(i => i.InterviewStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Skill configuration ----
            builder.Entity<Skill>(entity =>
            {
                entity.Property(s => s.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.HasIndex(s => s.Name)
                      .IsUnique();
            });

            // ---- Seeker relationships ----
            // Deleting a Seeker cascades to their skills (junction rows)
            builder.Entity<SeekerSkill>()
                .HasOne(ss => ss.Seeker)
                .WithMany(s => s.SeekerSkills)
                .HasForeignKey(ss => ss.SeekerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a Seeker cascades to their exclusively-owned address
            builder.Entity<Seeker>()
                .HasOne(s => s.Address)
                .WithOne()
                .HasForeignKey<Seeker>(s => s.AddressId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Seeker>()
                .HasIndex(s => s.AddressId)
                .IsUnique();

            // ---- Employer relationships ----
            builder.Entity<Employer>()
                .HasOne(e => e.Address)
                .WithOne()
                .HasForeignKey<Employer>(e => e.AddressId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Employer>()
                .HasIndex(e => e.AddressId)
                .IsUnique();

            // ---- ApplicationUser cascade delete chain ----
            // When the ApplicationUser is deleted (via UserManager in the service layer),
            // EF/DB cascades remove these owned child rows automatically.

            // UserNotification → ApplicationUser (cascade)
            builder.Entity<UserNotification>(entity =>
            {
                entity.HasIndex(un => new { un.UserId, un.IsHidden, un.IsRead });
                entity.HasOne(un => un.Notification)
                      .WithMany(n => n.UserNotifications)
                      .HasForeignKey(un => un.NotificationId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(un => un.User)
                      .WithMany(u => u.UserNotifications)
                      .HasForeignKey(un => un.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // DeviceToken → ApplicationUser (cascade)
            builder.Entity<DeviceToken>(entity =>
            {
                entity.Property(dt => dt.Token).IsRequired().HasMaxLength(500);
                entity.Property(dt => dt.DeviceType).IsRequired().HasMaxLength(50);
                entity.HasIndex(dt => dt.Token).IsUnique();
                entity.HasIndex(dt => dt.UserId);
                entity.HasOne(dt => dt.User)
                      .WithMany(u => u.DeviceTokens)
                      .HasForeignKey(dt => dt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Feedback → ApplicationUser (cascade)
            builder.Entity<Feedback>()
                .HasOne(f => f.AppUser)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.ReviewerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---- Notification configuration ----
            builder.Entity<Notification>(entity =>
            {
                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Body).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Topic).HasMaxLength(200);
                entity.Property(n => n.ActionUrl).HasMaxLength(500);
                entity.Property(n => n.ImageUrl).HasMaxLength(500);
                entity.HasIndex(n => n.CreatedAt);
                entity.HasIndex(n => n.Type);
            });


            // Seed data for ApplicationStatus
            builder.Entity<ApplicationStatus>().HasData(
                Enum.GetValues(typeof(ApplicationStatusEnum))
                    .Cast<ApplicationStatusEnum>()
                    .Select(e => new ApplicationStatus
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for InterviewStatus
            builder.Entity<InterviewStatus>().HasData(
                Enum.GetValues(typeof(InterviewStatusEnum))
                    .Cast<InterviewStatusEnum>()
                    .Select(e => new InterviewStatus
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for Currency
            builder.Entity<Currency>().HasData(
                Enum.GetValues(typeof(CurrencyEnum))
                    .Cast<CurrencyEnum>()
                    .Select(e => new Currency
                    {
                        Id = (int)e,
                        Code = e.ToString(),
                        Name = e.ToString(),
                    })
            );

            // Seed data for FeedbackType
            builder.Entity<FeedbackType>().HasData(
                Enum.GetValues(typeof(FeedbackTypeEnum))
                    .Cast<FeedbackTypeEnum>()
                    .Select(e => new FeedbackType
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for InterviewType
            builder.Entity<InterviewType>().HasData(
                Enum.GetValues(typeof(InterviewTypeEnum))
                    .Cast<InterviewTypeEnum>()
                    .Select(e => new InterviewType
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for JobLocationType
            builder.Entity<JobLocationType>().HasData(
                Enum.GetValues(typeof(JobLocationTypeEnum))
                    .Cast<JobLocationTypeEnum>()
                    .Select(e => new JobLocationType
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for JobType
            builder.Entity<JobType>().HasData(
                Enum.GetValues(typeof(JobTypeEnum))
                    .Cast<JobTypeEnum>()
                    .Select(e => new JobType
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for JobStatus
            builder.Entity<JobStatus>().HasData(
                Enum.GetValues(typeof(JobStatusEnum))
                    .Cast<JobStatusEnum>()
                    .Select(e => new JobStatus
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            // Seed data for EmployerStatus
            builder.Entity<EmployerStatus>().HasData(
                Enum.GetValues(typeof(EmployerStatusEnum))
                    .Cast<EmployerStatusEnum>()
                    .Select(e => new EmployerStatus
                    {
                        Id = (int)e,
                        Name = e.ToString(),
                        SortOrder = (int)e * 10
                    })
            );

            builder.Entity<IdentityRole<int>>().HasData(
                new IdentityRole<int>
                {
                    Id = 1,
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole<int>
                {
                    Id = 2,
                    Name = "Candidate",
                    NormalizedName = "CANDIDATE"
                },
                new IdentityRole<int>
                {
                    Id = 3,
                    Name = "Company",
                    NormalizedName = "COMPANY"
                }
            );

        }

        
    }
}
