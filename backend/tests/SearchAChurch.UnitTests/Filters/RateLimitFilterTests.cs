using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using SearchAChurch.Api.Features.Auth.Models;
using SearchAChurch.Api.Filters;
using SearchAChurch.Api.Services;
using Xunit;

namespace SearchAChurch.UnitTests.Filters;

[Trait("Category", "Unit")]
public class RateLimitFilterTests
{
    private readonly Mock<IRateLimiterService> _mockRateLimiter = new();
    private readonly Mock<ITokenService> _mockTokenService = new();

    private class TestEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; }
        public override IList<object?> Arguments { get; }

        public TestEndpointFilterInvocationContext(HttpContext httpContext, params object?[] arguments)
        {
            HttpContext = httpContext;
            Arguments = arguments.ToList();
        }

        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }

    [Fact]
    public void PredefinedPolicies_MatchSpecificationValues()
    {
        var registerPolicy = RateLimitPolicies.GetPolicy("register");
        Assert.Equal(3, registerPolicy.Limit);
        Assert.Equal(TimeSpan.FromHours(1), registerPolicy.Window);
        Assert.Equal(RateLimitKeyType.ClientIp, registerPolicy.KeyType);

        var loginPolicy = RateLimitPolicies.GetPolicy("login");
        Assert.Equal(5, loginPolicy.Limit);
        Assert.Equal(TimeSpan.FromMinutes(15), loginPolicy.Window);
        Assert.Equal(RateLimitKeyType.IpAndEmail, loginPolicy.KeyType);

        var loginGlobalPolicy = RateLimitPolicies.GetPolicy("login-global");
        Assert.Equal(50, loginGlobalPolicy.Limit);
        Assert.Equal(TimeSpan.FromHours(1), loginGlobalPolicy.Window);
        Assert.Equal(RateLimitKeyType.ClientIp, loginGlobalPolicy.KeyType);

        var refreshPolicy = RateLimitPolicies.GetPolicy("refresh");
        Assert.Equal(20, refreshPolicy.Limit);
        Assert.Equal(TimeSpan.FromMinutes(1), refreshPolicy.Window);
        Assert.Equal(RateLimitKeyType.UserAndDevice, refreshPolicy.KeyType);

        var forgotPasswordPolicy = RateLimitPolicies.GetPolicy("forgot-password");
        Assert.Equal(3, forgotPasswordPolicy.Limit);
        Assert.Equal(TimeSpan.FromHours(1), forgotPasswordPolicy.Window);
        Assert.Equal(RateLimitKeyType.IpAndEmail, forgotPasswordPolicy.KeyType);

        var resetPasswordPolicy = RateLimitPolicies.GetPolicy("reset-password");
        Assert.Equal(3, resetPasswordPolicy.Limit);
        Assert.Equal(TimeSpan.FromMinutes(15), resetPasswordPolicy.Window);
        Assert.Equal(RateLimitKeyType.IpAndEmail, resetPasswordPolicy.KeyType);
    }

    [Fact]
    public void ExtractIdentifier_WithClientIp_ExtractsDirectRemoteIp()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.195");
        var context = new TestEndpointFilterInvocationContext(httpContext);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.ClientIp);

        Assert.Equal("203.0.113.195", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithClientIp_PrefersXForwardedForHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.42, 10.0.0.1";
        var context = new TestEndpointFilterInvocationContext(httpContext);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.ClientIp);

        Assert.Equal("198.51.100.42", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithIpAndEmail_FromLoginRequest_NormalizesEmailToLowercase()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");
        var loginRequest = new LoginRequest("  Pastor.John@CHURCH.Org  ", "Password123!", "device-abc");
        var context = new TestEndpointFilterInvocationContext(httpContext, loginRequest);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.IpAndEmail);

        Assert.Equal("ip:192.168.1.50:email:pastor.john@church.org", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithIpAndEmail_FromForgotPasswordRequest_ReturnsComposite()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var forgotRequest = new ForgotPasswordRequest("Member@Domain.com");
        var context = new TestEndpointFilterInvocationContext(httpContext, forgotRequest);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.IpAndEmail);

        Assert.Equal("ip:127.0.0.1:email:member@domain.com", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithIpAndEmail_FromResetPasswordRequest_ReturnsComposite()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var resetRequest = new ResetPasswordRequest("Admin@Domain.com", "123456", "NewPassword123!");
        var context = new TestEndpointFilterInvocationContext(httpContext, resetRequest);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.IpAndEmail);

        Assert.Equal("ip:127.0.0.1:email:admin@domain.com", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithIpAndEmail_WhenNoEmailProvided_FallsBackToIpOnly()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        var context = new TestEndpointFilterInvocationContext(httpContext);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.IpAndEmail);

        Assert.Equal("ip:192.168.1.1", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithUserAndDevice_FromAuthenticatedClaims_ReturnsComposite()
    {
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var refreshRequest = new RefreshTokenRequest("raw-refresh-token", "device-ios-123");
        var context = new TestEndpointFilterInvocationContext(httpContext, refreshRequest);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.UserAndDevice);

        Assert.Equal($"user:{userId}:device:device-ios-123", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithUserAndDevice_FromExpiredBearerToken_ResolvesViaTokenService()
    {
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim("sub", userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _mockTokenService.Setup(x => x.GetPrincipalFromExpiredToken("expired-jwt-token"))
            .Returns(principal);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer expired-jwt-token";
        httpContext.Request.Headers["X-Device-Id"] = "device-android-999";
        var context = new TestEndpointFilterInvocationContext(httpContext);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.UserAndDevice, _mockTokenService.Object);

        Assert.Equal($"user:{userId}:device:device-android-999", identifier);
    }

    [Fact]
    public void ExtractIdentifier_WithUserAndDevice_WhenNoUserAvailable_FallsBackToDevice()
    {
        var httpContext = new DefaultHttpContext();
        var refreshRequest = new RefreshTokenRequest("token", "unregistered-device-1");
        var context = new TestEndpointFilterInvocationContext(httpContext, refreshRequest);

        var identifier = RateLimitFilter.ExtractIdentifier(context, RateLimitKeyType.UserAndDevice);

        Assert.Equal("device:unregistered-device-1", identifier);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimitAllowed_ExecutesNextAndSetsIetfHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.10");
        var context = new TestEndpointFilterInvocationContext(httpContext);

        _mockRateLimiter.Setup(x => x.CheckRateLimitAsync(
                "register",
                "10.0.0.10",
                3,
                TimeSpan.FromHours(1)))
            .ReturnsAsync(new RateLimitCheckResult(IsAllowed: true, RemainingRequests: 2, RetryAfterSeconds: 0));

        var filter = new RateLimitFilter(_mockRateLimiter.Object, "register");

        bool nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok("success"));
        };

        var result = await filter.InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.NotNull(result);
        Assert.Equal("3", httpContext.Response.Headers["RateLimit-Limit"].ToString());
        Assert.Equal("2", httpContext.Response.Headers["RateLimit-Remaining"].ToString());
        Assert.Equal("3600", httpContext.Response.Headers["RateLimit-Reset"].ToString());
        Assert.False(httpContext.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimitExceeded_ShortCircuitsWith429AndAllIetfHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
        var loginRequest = new LoginRequest("user@test.com", "wrong-password", "device-1");
        var context = new TestEndpointFilterInvocationContext(httpContext, loginRequest);

        _mockRateLimiter.Setup(x => x.CheckRateLimitAsync(
                "login",
                "ip:192.168.1.100:email:user@test.com",
                5,
                TimeSpan.FromMinutes(15)))
            .ReturnsAsync(new RateLimitCheckResult(IsAllowed: false, RemainingRequests: 0, RetryAfterSeconds: 450));

        var filter = new RateLimitFilter(_mockRateLimiter.Object, "login");

        bool nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok("should_not_be_called"));
        };

        var result = await filter.InvokeAsync(context, next);

        Assert.False(nextCalled);
        Assert.NotNull(result);
        Assert.IsType<JsonHttpResult<RateLimitExceededResponse>>(result);

        var jsonResult = (JsonHttpResult<RateLimitExceededResponse>)result;
        Assert.Equal(StatusCodes.Status429TooManyRequests, jsonResult.StatusCode);
        Assert.Equal("RATE_LIMIT_EXCEEDED", jsonResult.Value?.Error);
        Assert.Equal(450, jsonResult.Value?.RetryAfterSeconds);

        // Verify IETF Headers
        Assert.Equal("5", httpContext.Response.Headers["RateLimit-Limit"].ToString());
        Assert.Equal("0", httpContext.Response.Headers["RateLimit-Remaining"].ToString());
        Assert.Equal("450", httpContext.Response.Headers["RateLimit-Reset"].ToString());
        Assert.Equal("450", httpContext.Response.Headers["Retry-After"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenRetryAfterIsZeroOrNegativeOnExceeded_EnforcesMinimumOneSecond()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.10.10.10");
        var context = new TestEndpointFilterInvocationContext(httpContext);

        _mockRateLimiter.Setup(x => x.CheckRateLimitAsync(
                "register",
                "10.10.10.10",
                3,
                TimeSpan.FromHours(1)))
            .ReturnsAsync(new RateLimitCheckResult(IsAllowed: false, RemainingRequests: 0, RetryAfterSeconds: 0));

        var filter = new RateLimitFilter(_mockRateLimiter.Object, "register");
        EndpointFilterDelegate next = ctx => ValueTask.FromResult<object?>(Results.Ok());

        var result = await filter.InvokeAsync(context, next);

        Assert.IsType<JsonHttpResult<RateLimitExceededResponse>>(result);
        var jsonResult = (JsonHttpResult<RateLimitExceededResponse>)result;
        Assert.Equal(1, jsonResult.Value?.RetryAfterSeconds);

        Assert.Equal("1", httpContext.Response.Headers["Retry-After"].ToString());
        Assert.Equal("1", httpContext.Response.Headers["RateLimit-Reset"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithCustomPolicy_UsesCustomLimitAndWindow()
    {
        var customPolicy = new RateLimitPolicy("custom-action", RateLimitKeyType.ClientIp, 10, TimeSpan.FromSeconds(30));
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("1.1.1.1");
        var context = new TestEndpointFilterInvocationContext(httpContext);

        _mockRateLimiter.Setup(x => x.CheckRateLimitAsync(
                "custom-action",
                "1.1.1.1",
                10,
                TimeSpan.FromSeconds(30)))
            .ReturnsAsync(new RateLimitCheckResult(IsAllowed: true, RemainingRequests: 9, RetryAfterSeconds: 0));

        var filter = new RateLimitFilter(_mockRateLimiter.Object, customPolicy);

        bool nextCalled = false;
        EndpointFilterDelegate next = ctx =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var result = await filter.InvokeAsync(context, next);

        Assert.True(nextCalled);
        Assert.Equal("10", httpContext.Response.Headers["RateLimit-Limit"].ToString());
        Assert.Equal("9", httpContext.Response.Headers["RateLimit-Remaining"].ToString());
    }
}
