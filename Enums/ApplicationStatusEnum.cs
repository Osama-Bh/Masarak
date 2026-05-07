namespace GoWork.Enums
{
    public enum ApplicationStatusEnum
    {
        PendingReview = 1,   // New application, not reviewed yet
        //Shortlisted = 2,   // Passed initial screening
        Rejected = 3,   // Not selected
        Accepted = 4,   // Approved to move forward (e.g., to interview)
        //OfferExtended = 5,   // Job offer sent
        Hired = 6,   // Candidate accepted offer and is hired,
        Withdrawn = 7,    // Candidate withdrew the application
        Interview = 8,   // Application is in interview stage
    }

}
