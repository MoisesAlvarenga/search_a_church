using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SearchAChurch.Api.Authorization;
using Xunit;

namespace SearchAChurch.UnitTests.Authorization;

[Trait("Category", "Unit")]
public class ChurchRepresentativeAuthorizationTests
{
    private readonly ChurchRepresentativeAuthorizationHandler _handler = new();
    private readonly ChurchRepresentativeRequirement _requirement = new();

    [Fact]
    public async Task HandleRequirementAsync_WhenClaimIsTrue_Succeeds()
    {
        var claims = new[] { new Claim("is_verified_representative", "true") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenClaimIsFalse_DoesNotSucceed()
    {
        var claims = new[] { new Claim("is_verified_representative", "false") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenClaimIsMissing_DoesNotSucceed()
    {
        var claims = new[] { new Claim("sub", Guid.NewGuid().ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenClaimIsNotBoolean_DoesNotSucceed()
    {
        var claims = new[] { new Claim("is_verified_representative", "not_a_bool") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { _requirement }, principal, null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
