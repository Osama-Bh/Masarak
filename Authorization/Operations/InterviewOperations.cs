using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace GoWork.Authorization.Operations
{
    public class InterviewOperations
    {
        public static OperationAuthorizationRequirement Confirm = new() { Name = nameof(Confirm) };
        public static OperationAuthorizationRequirement Withdrawn = new() { Name = nameof(Withdrawn) };

    }
}
