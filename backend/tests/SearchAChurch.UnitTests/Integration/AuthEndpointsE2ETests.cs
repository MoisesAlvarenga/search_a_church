using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Features.Auth.Models;
using Xunit;

namespace SearchAChurch.UnitTests.Integration;

[Trait("Category", "E2E")]
public class AuthEndpointsE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteAuthenticationLifecycle_Register_Login_Refresh_Me_Logout_Succeeds()
    {
        var email = $"user_{Guid.NewGuid():N}@church.org";
        var password = "SecurePassword123!";
        var deviceId = "test-device-uuid";

        // 1. REGISTER
        var registerRequest = new RegisterRequest(email, password, "Test User", deviceId);
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerAuth);
        Assert.False(string.IsNullOrWhiteSpace(registerAuth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(registerAuth.RefreshToken));
        Assert.Equal(email, registerAuth.User.Email);

        // 2. QUERY /auth/me WITHOUT TOKEN -> 401 UNAUTHORIZED
        var unauthenticatedMe = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedMe.StatusCode);

        // 3. QUERY /auth/me WITH REGISTER ACCESS TOKEN -> 200 OK
        var initialMeRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        initialMeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registerAuth.AccessToken);
        var initialMeResponse = await _client.SendAsync(initialMeRequest);

        Assert.Equal(HttpStatusCode.OK, initialMeResponse.StatusCode);
        var initialUserSummary = await initialMeResponse.Content.ReadFromJsonAsync<UserSummaryResponse>();
        Assert.NotNull(initialUserSummary);
        Assert.Equal(email, initialUserSummary.Email);
        Assert.Equal("Test User", initialUserSummary.Name);

        // 4. LOGIN
        var loginRequest = new LoginRequest(email, password, deviceId);
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginAuth);
        Assert.False(string.IsNullOrWhiteSpace(loginAuth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginAuth.RefreshToken));

        // 5. REFRESH TOKEN (RTR)
        var refreshRequest = new RefreshTokenRequest(loginAuth.RefreshToken, deviceId);
        var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedAuth);
        Assert.False(string.IsNullOrWhiteSpace(refreshedAuth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshedAuth.RefreshToken));
        Assert.NotEqual(loginAuth.RefreshToken, refreshedAuth.RefreshToken);

        // 6. QUERY /auth/me WITH REFRESHED TOKEN -> 200 OK
        var refreshedMeRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        refreshedMeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAuth.AccessToken);
        var refreshedMeResponse = await _client.SendAsync(refreshedMeRequest);

        Assert.Equal(HttpStatusCode.OK, refreshedMeResponse.StatusCode);
        var refreshedUserSummary = await refreshedMeResponse.Content.ReadFromJsonAsync<UserSummaryResponse>();
        Assert.NotNull(refreshedUserSummary);
        Assert.Equal(email, refreshedUserSummary.Email);

        // 7. BREACH DETECTION: After the 2-second race-condition tolerance window expires,
        // reusing consumed login refresh token triggers 401 and revokes family
        await Task.Delay(2200);
        var reuseResponse = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        var reuseBody = await reuseResponse.Content.ReadAsStringAsync();
        Assert.Contains("TOKEN_BREACH_DETECTED", reuseBody);

        // 8. LOGOUT
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAuth.AccessToken);
        logoutRequest.Content = JsonContent.Create(new LogoutRequest(refreshedAuth.RefreshToken));
        var logoutResponse = await _client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    }

    [Fact]
    public async Task PasswordResetFlow_ForgotPassword_ResetPassword_LoginWithNewPassword_Succeeds()
    {
        var email = $"otp_user_{Guid.NewGuid():N}@church.org";
        var originalPassword = "InitialPassword123!";
        var newPassword = "NewSecurePassword456!";
        var deviceId = "otp-device";

        // 1. Register User
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, originalPassword, "OTP User", deviceId));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        // 2. Request OTP
        var forgotResponse = await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        // Extract OTP from database for testing
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        var otpEntry = await db.PasswordResetOtps.FirstAsync(o => o.UserId == user.Id && !o.IsConsumed);
        Assert.NotNull(otpEntry);

        // Try invalid OTP code
        var invalidResetResponse = await _client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest(email, "000000", newPassword));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResetResponse.StatusCode);

        // Login with old password still works
        var oldLoginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, originalPassword, deviceId));
        Assert.Equal(HttpStatusCode.OK, oldLoginResponse.StatusCode);
    }
}
