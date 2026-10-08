using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Extensions;
using SearchAChurch.Api.Features.Profile.Authorization;

namespace SearchAChurch.UnitTests.Features.Profile.Authorization;

[Trait("Category", "Unit")]
public class ProfilePolicyRegistrationTests
{
    [Fact]
    public async Task AddJwtAuthentication_ShouldRegisterProfilePoliciesAndHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Secret", "SuperSecretKeyForSearchAChurchProjectWithMin256BitsLength!"},
            {"Jwt:Issuer", "SearchAChurch.Api"},
            {"Jwt:Audience", "SearchAChurch.Client"},
            {"Jwt:AccessTokenExpirationMinutes", "15"},
            {"Jwt:RefreshTokenExpirationDays", "60"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<AppDbContext>();
        services.AddJwtAuthentication(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var ownershipPolicy = await policyProvider.GetPolicyAsync(ProfileAuthorizationPolicies.RequireProfileOwnership);
        ownershipPolicy.Should().NotBeNull();
        ownershipPolicy!.Requirements.Should().Contain(r => r is ProfileOwnershipRequirement);

        var verifiedRepPolicy = await policyProvider.GetPolicyAsync(ProfileAuthorizationPolicies.RequireVerifiedRepresentative);
        verifiedRepPolicy.Should().NotBeNull();
        verifiedRepPolicy!.Requirements.Should().Contain(r => r is VerifiedRepresentativeRequirement);

        var handlers = serviceProvider.GetServices<IAuthorizationHandler>().ToList();
        handlers.Should().Contain(h => h is OwnershipAuthorizationHandler);
        handlers.Should().Contain(h => h is VerifiedRepresentativeAuthorizationHandler);
    }
}
