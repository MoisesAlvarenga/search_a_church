using Microsoft.AspNetCore.Authorization;

namespace SearchAChurch.Api.Authorization;

public class ChurchRepresentativeRequirement : IAuthorizationRequirement
{
}

public class ChurchRepresentativeAuthorizationHandler : AuthorizationHandler<ChurchRepresentativeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ChurchRepresentativeRequirement requirement)
    {
        var claim = context.User.FindFirst("is_verified_representative");
        if (claim != null && bool.TryParse(claim.Value, out var isVerified) && isVerified)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
