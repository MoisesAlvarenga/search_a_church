using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.UnitTests.Data;

[Trait("Category", "Unit")]
public class ChurchEntityTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Church_DefaultValues_ShouldBeSetCorrectly()
    {
        // Act
        var church = new Church();

        // Assert
        church.Id.Should().NotBeEmpty();
        church.IsVerified.Should().BeFalse();
        church.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        church.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        church.DeletedAt.Should().BeNull();
        church.PlaceId.Should().BeNull();
        church.Name.Should().BeEmpty();
        church.FormattedAddress.Should().BeEmpty();
    }

    [Fact]
    public async Task Church_CanBeAddedAndRetrieved_FromDatabase()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Batista Central",
            FormattedAddress = "Av. Paulista, 1000 - Bela Vista, São Paulo - SP",
            PlaceId = "ChIJ456CentralBatista",
            Latitude = -23.5614,
            Longitude = -46.6558,
            Phone = "(11) 3333-4444",
            Website = "https://batistacentral.org.br",
            IsVerified = true
        };

        // Act
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var retrieved = await context.Churches.FirstOrDefaultAsync(c => c.Id == church.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Igreja Batista Central");
        retrieved.PlaceId.Should().Be("ChIJ456CentralBatista");
        retrieved.Latitude.Should().Be(-23.5614);
        retrieved.Longitude.Should().Be(-46.6558);
        retrieved.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Church_SoftDelete_WhenDeletedAtSet_ShouldBeFilteredOutFromQueries()
    {
        // Arrange
        using var context = CreateContext();
        var activeChurch = new Church
        {
            Name = "Igreja Presbiteriana Ativa",
            FormattedAddress = "Rua Ativa, 100",
            PlaceId = "ChIJ_Active",
            Latitude = -23.50,
            Longitude = -46.60
        };
        var deletedChurch = new Church
        {
            Name = "Igreja Excluída LGPD",
            FormattedAddress = "Rua Excluída, 200",
            PlaceId = "ChIJ_Deleted",
            Latitude = -23.51,
            Longitude = -46.61,
            DeletedAt = DateTimeOffset.UtcNow
        };

        context.Churches.AddRange(activeChurch, deletedChurch);
        await context.SaveChangesAsync();

        // Act
        var churches = await context.Churches.ToListAsync();

        // Assert
        churches.Should().ContainSingle();
        churches[0].Name.Should().Be("Igreja Presbiteriana Ativa");
    }

    [Fact]
    public async Task Church_SoftDelete_WhenIgnoreQueryFilters_ShouldReturnDeletedChurches()
    {
        // Arrange
        using var context = CreateContext();
        var deletedChurch = new Church
        {
            Name = "Igreja Removida",
            FormattedAddress = "Rua Removida, 300",
            PlaceId = "ChIJ_Removida",
            Latitude = -23.52,
            Longitude = -46.62,
            DeletedAt = DateTimeOffset.UtcNow
        };

        context.Churches.Add(deletedChurch);
        await context.SaveChangesAsync();

        // Act
        var churches = await context.Churches.IgnoreQueryFilters().ToListAsync();

        // Assert
        churches.Should().ContainSingle();
        churches[0].Name.Should().Be("Igreja Removida");
    }

    [Fact]
    public void Church_ModelConfiguration_ShouldConfigureIndexesAndKeys()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Church));

        // Assert
        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("churches");

        var primaryKey = entityType.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle(p => p.Name == nameof(Church.Id));

        // AD-004: Unique partial index on PlaceId
        var placeIdIndex = entityType.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Church.PlaceId)));
        placeIdIndex.Should().NotBeNull();
        placeIdIndex!.IsUnique.Should().BeTrue();
        placeIdIndex.GetFilter().Should().Be("place_id IS NOT NULL");

        // Spatial Coordinates composite index
        var coordsIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Any(p => p.Name == nameof(Church.Latitude)) &&
            i.Properties.Any(p => p.Name == nameof(Church.Longitude)));
        coordsIndex.Should().NotBeNull();

        // Required properties
        entityType.FindProperty(nameof(Church.Name))!.IsNullable.Should().BeFalse();
        entityType.FindProperty(nameof(Church.FormattedAddress))!.IsNullable.Should().BeFalse();
        entityType.FindProperty(nameof(Church.Name))!.GetMaxLength().Should().Be(200);
        entityType.FindProperty(nameof(Church.FormattedAddress))!.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public async Task Church_VerifiedByUser_Relationship_ShouldWorkCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var representative = new User
        {
            Email = "pastor@batistacentral.org.br",
            PasswordHash = "hash123",
            Name = "Pastor Lucas",
            Role = UserRole.ChurchRep,
            IsVerifiedRepresentative = true
        };
        context.Users.Add(representative);
        await context.SaveChangesAsync();

        var church = new Church
        {
            Name = "Igreja Batista Central",
            FormattedAddress = "Av. Paulista, 1000",
            PlaceId = "ChIJ_Batista_Central",
            Latitude = -23.56,
            Longitude = -46.65,
            IsVerified = true,
            VerifiedByUserId = representative.Id
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.Churches
            .Include(c => c.VerifiedByUser)
            .FirstOrDefaultAsync(c => c.Id == church.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.VerifiedByUser.Should().NotBeNull();
        retrieved.VerifiedByUser!.Email.Should().Be("pastor@batistacentral.org.br");
        retrieved.VerifiedByUser.IsVerifiedRepresentative.Should().BeTrue();
    }
}
