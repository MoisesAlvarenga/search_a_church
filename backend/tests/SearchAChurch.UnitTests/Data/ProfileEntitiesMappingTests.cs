using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.UnitTests.Data;

[Trait("Category", "Unit")]
public class ProfileEntitiesMappingTests
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

    [Fact]
    public void UserProfile_DefaultValues_ShouldBeSetCorrectly()
    {
        // Act
        var profile = new UserProfile();

        // Assert
        profile.Id.Should().NotBeEmpty();
        profile.DefaultRadiusKm.Should().Be(10.0);
        profile.IsAnonymous.Should().BeFalse();
        profile.PreferredLanguages.Should().ContainSingle().Which.Should().Be("pt");
        profile.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        profile.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        profile.DeletedAt.Should().BeNull();
        profile.Denomination.Should().BeNull();
        profile.WorshipStyle.Should().BeNull();
        profile.UserProfileTags.Should().BeEmpty();
    }

    [Fact]
    public async Task UserProfile_CanBeAddedAndRetrieved_WithUserRelationship()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User
        {
            Email = "member@searchachurch.com",
            PasswordHash = "hashed_pass",
            Name = "Membro Fiel",
            Role = UserRole.User
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var profile = new UserProfile
        {
            UserId = user.Id,
            Denomination = "Batista",
            WorshipStyle = "Contemporâneo",
            PreferredLanguages = new List<string> { "pt", "en" },
            DefaultRadiusKm = 15.0
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.UserProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == profile.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Should().Be(user.Id);
        retrieved.User.Should().NotBeNull();
        retrieved.User.Email.Should().Be("member@searchachurch.com");
        retrieved.Denomination.Should().Be("Batista");
        retrieved.WorshipStyle.Should().Be("Contemporâneo");
        retrieved.PreferredLanguages.Should().BeEquivalentTo(new[] { "pt", "en" });
        retrieved.DefaultRadiusKm.Should().Be(15.0);
        retrieved.IsAnonymous.Should().BeFalse();
    }

    [Fact]
    public void UserProfile_ModelConfiguration_ShouldHaveUniqueIndexOnUserId()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(UserProfile));

        // Assert
        entityType.Should().NotBeNull();
        var index = entityType!.FindIndex(entityType.FindProperty(nameof(UserProfile.UserId))!);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task UserProfile_SoftDeleteFilter_ShouldExcludeDeletedProfiles()
    {
        // Arrange
        using var context = CreateContext();
        var user1 = new User { Email = "u1@searchachurch.com", PasswordHash = "h", Name = "U1" };
        var user2 = new User { Email = "u2@searchachurch.com", PasswordHash = "h", Name = "U2" };
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        var activeProfile = new UserProfile { UserId = user1.Id, Denomination = "Metodista" };
        var deletedProfile = new UserProfile
        {
            UserId = user2.Id,
            Denomination = "Presbiteriana",
            DeletedAt = DateTimeOffset.UtcNow,
            IsAnonymous = true
        };
        context.UserProfiles.AddRange(activeProfile, deletedProfile);
        await context.SaveChangesAsync();

        // Act
        var profiles = await context.UserProfiles.ToListAsync();
        var allProfiles = await context.UserProfiles.IgnoreQueryFilters().ToListAsync();

        // Assert
        profiles.Should().ContainSingle();
        profiles[0].Denomination.Should().Be("Metodista");
        allProfiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task TagCatalog_SeedData_ShouldContain15OfficialSeedTags()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var seededTags = await context.TagCatalogs.ToListAsync();

        // Assert
        seededTags.Should().HaveCount(15);

        // Group by category
        var accessibilityTags = seededTags.Where(t => t.Category == TagCategory.Accessibility).ToList();
        var infrastructureTags = seededTags.Where(t => t.Category == TagCategory.Infrastructure).ToList();
        var ministriesTags = seededTags.Where(t => t.Category == TagCategory.Ministries).ToList();

        accessibilityTags.Should().HaveCount(5);
        infrastructureTags.Should().HaveCount(5);
        ministriesTags.Should().HaveCount(5);

        // Verify key codes
        var codes = seededTags.Select(t => t.Code).ToList();
        codes.Should().Contain(new[]
        {
            "rampa_acesso",
            "interprete_libras",
            "banheiro_acessivel",
            "elevador_acessivel",
            "audiodescricao",
            "estacionamento_proprio",
            "ar_condicionado",
            "espaco_kids_bercario",
            "transmissao_online",
            "refeitorio_cantina",
            "ministerio_jovens",
            "ministerio_infantil",
            "ministerio_casais",
            "escola_biblica",
            "acao_social"
        });
    }

    [Fact]
    public void TagCatalog_ModelConfiguration_ShouldHaveUniqueIndexOnCode()
    {
        // Arrange
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(TagCatalog));

        // Assert
        entityType.Should().NotBeNull();
        var index = entityType!.FindIndex(entityType.FindProperty(nameof(TagCatalog.Code))!);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task UserProfileTag_ManyToMany_ShouldPersistAndRelateCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var user = new User { Email = "taguser@test.com", PasswordHash = "h", Name = "Tag User" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var profile = new UserProfile { UserId = user.Id };
        context.UserProfiles.Add(profile);

        var tag = new TagCatalog
        {
            Code = "custom_tag_test",
            Name = "Tag Teste",
            Category = TagCategory.Accessibility,
            Description = "Descrição teste",
            IconName = "accessible"
        };
        context.TagCatalogs.Add(tag);
        await context.SaveChangesAsync();

        var link = new UserProfileTag
        {
            UserProfileId = profile.Id,
            TagId = tag.Id
        };
        context.UserProfileTags.Add(link);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.UserProfileTags
            .Include(pt => pt.UserProfile)
            .Include(pt => pt.Tag)
            .FirstOrDefaultAsync(pt => pt.UserProfileId == profile.Id && pt.TagId == tag.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.UserProfile.UserId.Should().Be(user.Id);
        retrieved.Tag.Code.Should().Be("custom_tag_test");
    }

    [Fact]
    public async Task ChurchTag_ManyToMany_ShouldPersistAndRelateCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Tag Test",
            FormattedAddress = "Rua Teste, 123",
            Latitude = -23.55,
            Longitude = -46.63
        };
        context.Churches.Add(church);

        var tag = new TagCatalog
        {
            Code = "church_tag_test",
            Name = "Church Tag",
            Category = TagCategory.Infrastructure,
            Description = "Infra teste",
            IconName = "local_parking"
        };
        context.TagCatalogs.Add(tag);
        await context.SaveChangesAsync();

        var link = new ChurchTag
        {
            ChurchId = church.Id,
            TagId = tag.Id
        };
        context.ChurchTags.Add(link);
        await context.SaveChangesAsync();

        // Act
        var retrieved = await context.ChurchTags
            .Include(ct => ct.Church)
            .Include(ct => ct.Tag)
            .FirstOrDefaultAsync(ct => ct.ChurchId == church.Id && ct.TagId == tag.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Church.Name.Should().Be("Igreja Tag Test");
        retrieved.Tag.Code.Should().Be("church_tag_test");
    }

    [Fact]
    public async Task ChurchMeetingSchedule_ShouldPersistAndLinkToChurch()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja com Cultos",
            FormattedAddress = "Av Principal, 500",
            Latitude = -23.50,
            Longitude = -46.60
        };
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var schedule1 = new ChurchMeetingSchedule
        {
            ChurchId = church.Id,
            DayOfWeek = DayOfWeek.Sunday,
            StartTime = "10:00",
            Description = "Culto Matutino & EBD",
            Language = "pt"
        };
        var schedule2 = new ChurchMeetingSchedule
        {
            ChurchId = church.Id,
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = "19:30",
            Description = "Culto de Oração",
            Language = "pt"
        };
        context.ChurchMeetingSchedules.AddRange(schedule1, schedule2);
        await context.SaveChangesAsync();

        // Act
        var retrievedSchedules = await context.ChurchMeetingSchedules
            .Where(s => s.ChurchId == church.Id)
            .OrderBy(s => s.DayOfWeek)
            .ToListAsync();

        // Assert
        retrievedSchedules.Should().HaveCount(2);
        retrievedSchedules[0].DayOfWeek.Should().Be(DayOfWeek.Sunday);
        retrievedSchedules[0].StartTime.Should().Be("10:00");
        retrievedSchedules[0].Description.Should().Be("Culto Matutino & EBD");
        retrievedSchedules[0].Language.Should().Be("pt");

        retrievedSchedules[1].DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        retrievedSchedules[1].StartTime.Should().Be("19:30");
    }

    [Fact]
    public async Task Church_EnrichedAttributes_ShouldDefaultAndPersistCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var church = new Church
        {
            Name = "Igreja Presbiteriana Renovada",
            FormattedAddress = "Rua das Oliveiras, 77",
            Latitude = -22.90,
            Longitude = -43.20,
            Denomination = "Presbiteriana",
            WorshipStyle = "Tradicional",
            Languages = new List<string> { "pt", "en" },
            Email = "contato@ipr.org.br",
            SocialInstagram = "@iprenovada",
            SocialFacebook = "iprenovadaoficial",
            IsActive = true
        };

        // Assert initial defaults before save
        church.IsActive.Should().BeTrue();
        church.ConcurrencyStamp.Should().NotBeNullOrWhiteSpace();
        church.MeetingSchedules.Should().BeEmpty();
        church.ChurchTags.Should().BeEmpty();

        // Act
        context.Churches.Add(church);
        await context.SaveChangesAsync();

        var retrieved = await context.Churches.FirstOrDefaultAsync(c => c.Id == church.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Denomination.Should().Be("Presbiteriana");
        retrieved.WorshipStyle.Should().Be("Tradicional");
        retrieved.Languages.Should().BeEquivalentTo(new[] { "pt", "en" });
        retrieved.Email.Should().Be("contato@ipr.org.br");
        retrieved.SocialInstagram.Should().Be("@iprenovada");
        retrieved.SocialFacebook.Should().Be("iprenovadaoficial");
        retrieved.IsActive.Should().BeTrue();
        retrieved.ConcurrencyStamp.Should().Be(church.ConcurrencyStamp);
    }
}
