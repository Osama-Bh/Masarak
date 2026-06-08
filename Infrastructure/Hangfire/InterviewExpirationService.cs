using GoWork.Data;
using GoWork.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoWork.Infrastructure.Hangfire
{
    public class InterviewExpirationService
    {
        private readonly ApplicationDbContext _context;

        public InterviewExpirationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ExpireInterview(int interviewId)
        {
            var interview = await _context.TbInterviews
                .Include(i => i.Application)
                .FirstOrDefaultAsync(i => i.Id == interviewId);

            if (interview == null)
                return;

            // Only expire Scheduled interviews when they reach their scheduled date/time
            if (interview.InterviewStatusId != (int)InterviewStatusEnum.Scheduled)
                return;

            interview.InterviewStatusId = (int)InterviewStatusEnum.MissingInterview;
            interview.Application.ApplicationStatusId = (int)ApplicationStatusEnum.MissingInterview;

            await _context.SaveChangesAsync();
        }
    }
}
