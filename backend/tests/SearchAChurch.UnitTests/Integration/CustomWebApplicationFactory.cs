using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Services;

namespace SearchAChurch.UnitTests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Register InMemory database for integration testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Replace Redis RateLimiter with mock that allows requests
            services.RemoveAll<IRateLimiterService>();
            var mockLimiter = new Mock<IRateLimiterService>();
            mockLimiter.Setup(x => x.CheckRateLimitAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(new RateLimitCheckResult(true, 100, 0));
            services.AddSingleton(mockLimiter.Object);
        });
    }
}
