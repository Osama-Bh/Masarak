using GoWork.Data;
using GoWork.Enums;
using GoWork.Models;

namespace GoWork.Infrastructure.Hangfire
{
    public class JobExpirationService
    {
        private readonly ApplicationDbContext _context;

        public JobExpirationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ExpireJob(int jobId)
        {
            var job = await _context.TbJobs.FindAsync(jobId);

            if (job == null)
                return;

            if (job.JobStatusId != (int)JobStatusEnum.Published)
                return;

            job.JobStatusId = (int)JobStatusEnum.Expired;

            await _context.SaveChangesAsync();
        }

        public void SayHello()
        {
            Console.WriteLine("Hangfire is working!");
        }
    }
}
