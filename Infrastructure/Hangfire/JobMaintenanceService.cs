using GoWork.Data;
using GoWork.Enums;
using GoWork.Models;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Infrastructure.Hangfire
{
    public class JobMaintenanceService
    {
        private readonly ApplicationDbContext _context;

        public JobMaintenanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ExpireMissedJobs()
        {
            var expiredJobs = await _context.TbJobs
                .Where(j => j.JobStatusId == (int)JobStatusEnum.Published && j.ExpirationDate <= DateTime.UtcNow).ToListAsync();


            foreach (var job in expiredJobs)
            {
                job.JobStatusId = (int)JobStatusEnum.Expired;
            }

            await _context.SaveChangesAsync();
        }
    }
}
