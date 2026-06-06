using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoWork.Models
{
    public class Job
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        public string Title { get; set; } = null!;
        [Required(ErrorMessage = "Description is required.")]
        [StringLength(700, ErrorMessage = "Description cannot exceed 100 characters.")]
        public string Description { get; set; } = null!;
        public int EmployerId { get; set; }
        public int CategoryId { get; set; }
        public int JobTypeId { get; set; }
        public int JobLocationTypeId { get; set; }
        public int AddressId { get; set; }
        [Range(0.01, 1000000.00, ErrorMessage = "Price must be between $0.01 and $1,000,000.00.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinSalary { get; set; }
        [Range(0.01, 1000000.00, ErrorMessage = "Price must be between $0.01 and $1,000,000.00.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxSalary { get; set; }
        public int CurrencyId { get; set; }
        public DateTime PostedDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int JobStatusId { get; set; }
        public string? ExpirationHangfireJobId { get; set; }

        // Navigation properties
        [ForeignKey("EmployerId")]
        public Employer Employer { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;
        [ForeignKey("JobTypeId")]
        public JobType JobType { get; set; } = null!;
        [ForeignKey("JobLocationTypeId")]
        public JobLocationType JobLocationType { get; set; } = null!;
        [ForeignKey("CurrencyId")]
        public Currency Currency { get; set; } = null!;
        public Address Address { get; set; } = null!;
        [ForeignKey("JobStatusId")]
        public JobStatus JobStatus { get; set; } = null!;

        public ICollection<JobSkill> JobSkills { get; set; } = null!;
        public ICollection<Application> Applications { get; set; } = null!;

    }
}
