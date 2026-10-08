using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Authorization;
using SecurityClaim = System.Security.Claims.Claim;

namespace SearchAChurch.UnitTests.Features.Profile.Authorization;

[Trait("Category", "Unit")]
public class OwnershipAuthorizationHandlerTests
{
    private static (OwnershipAuthorizationHandler handler, DefaultHttpContext httpContext) CreateHandler()
    {
        var httpContext = new DefaultHttpContext();
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var loggerMock = new Mock<ILogger<OwnershipAuthorizationHandler>>();
        var handler = new OwnershipAuthorizationHandler(accessorMock.Object, loggerMock.Object);

        return (handler, httpContext);
    }

    private static ClaimsPrincipal CreateUserPrincipal(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var identity = new ClaimsIdentity(new[]
        {
            new SecurityClaim("sub", userId.Value.ToString()),
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.Value.ToString())
        }, "TestAuthentication");

        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasMatchingSubAsGuidResource_ShouldSucceed()
    {
        // Arrange
        var (handler, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, userId);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasDifferentSubAsGuidResource_ShouldFailAndSetAcessoNegadoPropriedade()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var user = CreateUserPrincipal(currentUserId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, targetUserId);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("ACESSO_NEGADO_PROPRIEDADE");
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasMatchingSubAsStringResource_ShouldSucceed()
    {
        // Arrange
        var (handler, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, userId.ToString());

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasMatchingSubInUserProfileResource_ShouldSucceed()
    {
        // Arrange
        var (handler, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);
        var userProfile = new UserProfile { UserId = userId, Denomination = "Batista" };
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, userProfile);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasDifferentSubInUserProfileResource_ShouldFailAndSetAcessoNegadoPropriedade()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = CreateUserPrincipal(currentUserId);
        var userProfile = new UserProfile { UserId = otherUserId, Denomination = "Batista" };
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, userProfile);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("ACESSO_NEGADO_PROPRIEDADE");
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasMatchingSubInRouteValues_ShouldSucceed()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var userId = Guid.NewGuid();
        httpContext.Request.RouteValues["userId"] = userId.ToString();

        var user = CreateUserPrincipal(userId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasMatchingSubInRouteValuesWithIdKey_ShouldSucceed()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var userId = Guid.NewGuid();
        httpContext.Request.RouteValues["id"] = userId.ToString();

        var user = CreateUserPrincipal(userId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserHasDifferentSubInRouteValues_ShouldFailAndSetAcessoNegadoPropriedade()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        httpContext.Request.RouteValues["userId"] = targetUserId.ToString();

        var user = CreateUserPrincipal(currentUserId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("ACESSO_NEGADO_PROPRIEDADE");
    }

    [Fact]
    public async Task HandleAsync_WhenResourceIsNullAndNoRoute_ShouldSucceedForOwnProfile()
    {
        // Arrange
        var (handler, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotAuthenticatedOrMissingSub_ShouldFailAndSetAcessoNegadoPropriedade()
    {
        // Arrange
        var (handler, httpContext) = CreateHandler();
        var user = CreateUserPrincipal(null);
        var requirement = new ProfileOwnershipRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, Guid.NewGuid());

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("ACESSO_NEGADO_PROPRIEDADE");
    }
}
