using GoWork.Authorization.Operations;
using GoWork.Authorization.Requirements;
using GoWork.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;

namespace GoWork.Authorization.Handlers
{
    public class InterviewAuthorizationHandler : AuthorizationHandler<CandidateOwnInterviewRquirements, Interview >
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CandidateOwnInterviewRquirements requirement, Interview resource)
        {
            var claims = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (claims == null || !int.TryParse(claims.Value, out int id))
            {
                return Task.CompletedTask;
            }

            if (context.User.IsInRole("Candidate") && resource.Application.Seeker?.UserId == id)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}
