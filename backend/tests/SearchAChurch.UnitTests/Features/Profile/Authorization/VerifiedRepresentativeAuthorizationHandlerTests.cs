using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Profile.Authorization;
using SecurityClaim = System.Security.Claims.Claim;

namespace SearchAChurch.UnitTests.Features.Profile.Authorization;

[Trait("Category", "Unit")]
public class VerifiedRepresentativeAuthorizationHandlerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static (VerifiedRepresentativeAuthorizationHandler handler, DefaultHttpContext httpContext, AppDbContext dbContext) CreateHandler(AppDbContext? context = null)
    {
        var db = context ?? CreateContext();
        var httpContext = new DefaultHttpContext();
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var loggerMock = new Mock<ILogger<VerifiedRepresentativeAuthorizationHandler>>();
        var handler = new VerifiedRepresentativeAuthorizationHandler(db, accessorMock.Object, loggerMock.Object);

        return (handler, httpContext, db);
    }

    private static ClaimsPrincipal CreateUserPrincipal(Guid? userId, bool isVerifiedClaim = false)
    {
        if (!userId.HasValue)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<SecurityClaim>
        {
            new("sub", userId.Value.ToString()),
            new(ClaimTypes.NameIdentifier, userId.Value.ToString())
        };

        if (isVerifiedClaim)
        {
            claims.Add(new SecurityClaim("is_verified_representative", "true"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsVerifiedRepresentativeOfChurchEntity_ShouldSucceed()
    {
        // Arrange
        var (handler, _, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Central",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotRepresentativeOfChurchEntity_ShouldFailAndSetRepresentanteNaoVerificado()
    {
        // Arrange
        var (handler, httpContext, _) = CreateHandler();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = CreateUserPrincipal(currentUserId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Central",
            VerifiedByUserId = otherUserId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenChurchEntityIsNotVerified_ShouldFailAndSetRepresentanteNaoVerificado()
    {
        // Arrange
        var (handler, httpContext, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Pendente",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Pending_Verification,
            IsVerified = false,
            IsActive = true
        };

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenChurchEntityIsSoftDeleted_ShouldFailAndSetRepresentanteNaoVerificado()
    {
        // Arrange
        var (handler, httpContext, _) = CreateHandler();
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Excluída",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            DeletedAt = DateTimeOffset.UtcNow
        };

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenChurchIdResourceMatchesVerifiedRepresentativeInDatabase_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja no Banco",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church.Id);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenChurchIdResourceBelongsToAnotherRepresentativeInDatabase_ShouldFail()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = CreateUserPrincipal(currentUserId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja de Outro",
            VerifiedByUserId = otherUserId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church.Id);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenChurchDoesNotExistInDatabase_ShouldFail()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, Guid.NewGuid());

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenChurchIdAsStringResourceMatchesVerifiedRepresentativeInDatabase_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja String Id",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, church.Id.ToString());

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenRouteValuesContainChurchIdAndUserIsVerified_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Rota",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        httpContext.Request.RouteValues["churchId"] = church.Id.ToString();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenRouteValuesContainIdKeyAndUserIsVerified_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Rota Id",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        httpContext.Request.RouteValues["id"] = church.Id.ToString();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenRouteValuesContainChurchIdAndUserIsNotRepresentative_ShouldFail()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var user = CreateUserPrincipal(currentUserId);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Outro Rota",
            VerifiedByUserId = otherUserId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        httpContext.Request.RouteValues["id"] = church.Id.ToString();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenNoChurchSpecifiedAndUserHasClaimIsVerifiedRepresentative_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId, isVerifiedClaim: true);

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenNoChurchSpecifiedAndUserHasVerifiedChurchInDatabase_ShouldSucceed()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, _, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId, isVerifiedClaim: false);

        var church = new Church
        {
            Id = Guid.NewGuid(),
            Name = "Igreja Vinculada",
            VerifiedByUserId = userId,
            ClaimStatus = ChurchClaimState.Verified,
            IsVerified = true,
            IsActive = true
        };
        db.Churches.Add(church);
        await db.SaveChangesAsync();

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenNoChurchSpecifiedAndUserHasNoClaimAndNoVerifiedChurch_ShouldFail()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var userId = Guid.NewGuid();
        var user = CreateUserPrincipal(userId, isVerifiedClaim: false);

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }

    [Fact]
    public async Task HandleAsync_WhenUserNotAuthenticatedOrMissingSub_ShouldFail()
    {
        // Arrange
        using var db = CreateContext();
        var (handler, httpContext, _) = CreateHandler(db);
        var user = CreateUserPrincipal(null);

        var requirement = new VerifiedRepresentativeRequirement();
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        httpContext.Items["AuthorizationFailureCode"].Should().Be("REPRESENTANTE_NAO_VERIFICADO");
    }
}
