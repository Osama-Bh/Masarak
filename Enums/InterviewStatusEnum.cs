namespace GoWork.Enums
{
    public enum InterviewStatusEnum
    {
        Scheduled = 1,   // Interview date/time set
        Completed = 2,   // Interview finished
        Cancelled = 3,   // Cancelled by employer or seeker
        Rescheduled = 4,  // Moved to a new date/time
        NoShow = 5,    // Candidate did not attend
        Confirmed = 6,    // Candidate confirmed attendance
        MissingInterview = 7,   // Interview was scheduled but candidate did not attend
        Withdrawn = 8    // Candidate withdrew from the interview process
    }

}
