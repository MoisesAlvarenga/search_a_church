using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.UnitTests.Data;

[Trait("Category", "Unit")]
public class AppDbContextTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SoftDeleteFilter_WhenUserDeleted_ShouldBeFilteredOutFromQueries()
    {
        // Arrange
        using var context = CreateContext();
        var activeUser = new User
        {
            Email = "active@searchachurch.com",
            PasswordHash = "hash1",
            Name = "Active User",
            Role = UserRole.User
        };
        var deletedUser = new User
        {
            Email = "deleted@searchachurch.com",
            PasswordHash = "hash2",
            Name = "Deleted User",
            Role = UserRole.User,
            DeletedAt = DateTimeOffset.UtcNow
        };

        context.Users.AddRange(activeUser, deletedUser);
        await context.SaveChangesAsync();

        // Act
        var users = await context.Users.ToListAsync();

        // Assert
        users.Should().ContainSingle();
        users[0].Email.Should().Be("active@searchachurch.com");
    }

    [Fact]
    public async Task SoftDeleteFilter_WhenIgnoreQueryFilters_ShouldReturnDeletedUsers()
    {
        // Arrange
        using var context = CreateContext();
        var deletedUser = new User
        {
            Email = "deleted@searchachurch.com",
            PasswordHash = "hash",
            Name = "Deleted User",
            Role = UserRole.User,
            DeletedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(deletedUser);
        await context.SaveChangesAsync();

        // Act
        var allUsers = await context.Users.IgnoreQueryFilters().ToListAsync();

        // Assert
        allUsers.Should().ContainSingle();
        allUsers[0].DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshToken_WithUserRelationship_ShouldPersistSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "rep@searchachurch.com",
            PasswordHash = "hash",
            Name = "Church Representative",
            Role = UserRole.ChurchRep,
            IsVerifiedRepresentative = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var token = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "abc123sha256hash",
            DeviceId = "device-ios-1",
            Status = RefreshTokenStatus.Active,
            ClientIp = "127.0.0.1",
            UserAgent = "iOS App",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60)
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        // Act
        var persisted = await context.RefreshTokens.Include(rt => rt.User).FirstOrDefaultAsync();

        // Assert
        persisted.Should().NotBeNull();
        persisted!.User.Should().NotBeNull();
        persisted.User!.Email.Should().Be("rep@searchachurch.com");
    }

    [Fact]
    public async Task PasswordResetOtp_WithUserRelationship_ShouldPersistSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "reset@searchachurch.com",
            PasswordHash = "hash",
            Name = "User Reset",
            Role = UserRole.User
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            OtpHash = "otphash123",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        context.PasswordResetOtps.Add(otp);
        await context.SaveChangesAsync();

        // Act
        var persisted = await context.PasswordResetOtps.Include(o => o.User).FirstOrDefaultAsync();

        // Assert
        persisted.Should().NotBeNull();
        persisted!.User.Should().NotBeNull();
        persisted.User!.Name.Should().Be("User Reset");
    }

    [Fact]
    public async Task AuditLog_WithUserRelationship_ShouldPersistSuccessfully()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "audit@searchachurch.com",
            PasswordHash = "hash",
            Name = "Audited User",
            Role = UserRole.Admin
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var log = new AuditLog
        {
            UserId = user.Id,
            EventType = "ADMIN_ACTION",
            ClientIp = "192.168.1.50",
            ClientPort = 443,
            UserAgent = "AdminPanel/1.0",
            Metadata = "{\"action\":\"promote\"}"
        };
        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Act
        var persisted = await context.AuditLogs.Include(a => a.User).FirstOrDefaultAsync();

        // Assert
        persisted.Should().NotBeNull();
        persisted!.User.Should().NotBeNull();
        persisted.User!.Role.Should().Be(UserRole.Admin);
    }
}
