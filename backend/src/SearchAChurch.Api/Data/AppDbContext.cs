using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SearchAChurch.Api.Data.Entities;

namespace SearchAChurch.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Church> Churches => Set<Church>();
    public DbSet<ChurchClaim> ChurchClaims => Set<ChurchClaim>();
    public DbSet<ClaimEvidence> ClaimEvidences => Set<ClaimEvidence>();
    public DbSet<DisputeCase> DisputeCases => Set<DisputeCase>();
    public DbSet<ClaimAuditLog> ClaimAuditLogs => Set<ClaimAuditLog>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<TagCatalog> TagCatalogs => Set<TagCatalog>();
    public DbSet<UserProfileTag> UserProfileTags => Set<UserProfileTag>();
    public DbSet<ChurchTag> ChurchTags => Set<ChurchTag>();
    public DbSet<ChurchMeetingSchedule> ChurchMeetingSchedules => Set<ChurchMeetingSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v)
                ? new List<string>()
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? new List<string>() : c.ToList());

        // USERS table
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users");
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Email).IsUnique();

            builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
            builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            builder.Property(u => u.Name).HasMaxLength(150).IsRequired();
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50).IsRequired();

            // LGPD Soft Delete Filter
            builder.HasQueryFilter(u => u.DeletedAt == null);
        });

        // REFRESH_TOKENS table
        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.ToTable("refresh_tokens");
            builder.HasKey(rt => rt.Id);
            builder.HasIndex(rt => rt.TokenHash).IsUnique();
            builder.HasIndex(rt => rt.UserId);
            builder.HasIndex(rt => rt.FamilyId);

            builder.Property(rt => rt.TokenHash).HasMaxLength(128).IsRequired();
            builder.Property(rt => rt.DeviceId).HasMaxLength(100).IsRequired();
            builder.Property(rt => rt.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(rt => rt.ClientIp).HasMaxLength(45).IsRequired();
            builder.Property(rt => rt.UserAgent).HasMaxLength(500).IsRequired();

            builder.HasOne(rt => rt.User)
                   .WithMany(u => u.RefreshTokens)
                   .HasForeignKey(rt => rt.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(rt => rt.User.DeletedAt == null);
        });

        // PASSWORD_RESET_OTPS table
        modelBuilder.Entity<PasswordResetOtp>(builder =>
        {
            builder.ToTable("password_reset_otps");
            builder.HasKey(o => o.Id);
            builder.HasIndex(o => o.UserId);

            builder.Property(o => o.OtpHash).HasMaxLength(128).IsRequired();

            builder.HasOne(o => o.User)
                   .WithMany(u => u.PasswordResetOtps)
                   .HasForeignKey(o => o.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(o => o.User.DeletedAt == null);
        });

        // AUDIT_LOGS table
        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("audit_logs");
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => a.UserId);
            builder.HasIndex(a => a.TimestampUtc);

            builder.Property(a => a.EventType).HasMaxLength(50).IsRequired();
            builder.Property(a => a.ClientIp).HasMaxLength(45).IsRequired();
            builder.Property(a => a.UserAgent).HasMaxLength(500).IsRequired();
            builder.Property(a => a.Metadata).HasColumnType("jsonb");

            builder.HasOne(a => a.User)
                   .WithMany(u => u.AuditLogs)
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.SetNull);
        });

        // CHURCHES table
        modelBuilder.Entity<Church>(builder =>
        {
            builder.ToTable("churches");
            builder.HasKey(c => c.Id);

            // AD-004 / AD-025: Unique partial index on PlaceId
            builder.HasIndex(c => c.PlaceId)
                   .IsUnique()
                   .HasFilter("place_id IS NOT NULL");

            // Spatial Coordinates index
            builder.HasIndex(c => new { c.Latitude, c.Longitude });
            builder.HasIndex(c => c.ClaimStatus);
            builder.HasIndex(c => c.IsActive);

            builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
            builder.Property(c => c.FormattedAddress).HasMaxLength(500).IsRequired();
            builder.Property(c => c.PlaceId).HasMaxLength(255);
            builder.Property(c => c.Phone).HasMaxLength(50);
            builder.Property(c => c.Website).HasMaxLength(500);
            builder.Property(c => c.Email).HasMaxLength(255);
            builder.Property(c => c.SocialInstagram).HasMaxLength(100);
            builder.Property(c => c.SocialFacebook).HasMaxLength(100);
            builder.Property(c => c.Denomination).HasMaxLength(150);
            builder.Property(c => c.WorshipStyle).HasMaxLength(100);
            builder.Property(c => c.Languages).HasConversion(stringListConverter, stringListComparer).HasColumnType("jsonb").IsRequired();
            builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
            builder.Property(c => c.ConcurrencyStamp).HasMaxLength(100).IsRequired();
            builder.Property(c => c.ClaimStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(c => c.VerificationTier).HasConversion<string>().HasMaxLength(50).IsRequired();

            builder.HasOne(c => c.VerifiedByUser)
                   .WithMany()
                   .HasForeignKey(c => c.VerifiedByUserId)
                   .OnDelete(DeleteBehavior.SetNull);

            // LGPD Soft Delete Filter
            builder.HasQueryFilter(c => c.DeletedAt == null);
        });

        // CHURCH_CLAIMS table
        modelBuilder.Entity<ChurchClaim>(builder =>
        {
            builder.ToTable("church_claims");
            builder.HasKey(c => c.Id);

            builder.HasIndex(c => new { c.ChurchId, c.Status });
            builder.HasIndex(c => c.ExpiresAt);
            builder.HasIndex(c => c.UserId);

            builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(c => c.TargetTier).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(c => c.ValidationMethod).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(c => c.TosVersion).HasMaxLength(20).IsRequired();

            builder.HasOne(c => c.Church)
                   .WithMany(ch => ch.Claims)
                   .HasForeignKey(c => c.ChurchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.User)
                   .WithMany(u => u.ChurchClaims)
                   .HasForeignKey(c => c.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(c => c.Church.DeletedAt == null && c.User.DeletedAt == null);
        });

        // CLAIM_EVIDENCES table
        modelBuilder.Entity<ClaimEvidence>(builder =>
        {
            builder.ToTable("claim_evidences");
            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.ClaimId);

            builder.Property(e => e.EvidenceType).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(e => e.RawDataOrUrl).HasMaxLength(2000);
            builder.Property(e => e.FileHashSha256).HasMaxLength(128);
            builder.Property(e => e.Metadata).HasColumnType("jsonb");

            builder.HasOne(e => e.Claim)
                   .WithMany(c => c.Evidences)
                   .HasForeignKey(e => e.ClaimId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(e => e.Claim.Church.DeletedAt == null && e.Claim.User.DeletedAt == null);
        });

        // DISPUTE_CASES table
        modelBuilder.Entity<DisputeCase>(builder =>
        {
            builder.ToTable("dispute_cases");
            builder.HasKey(d => d.Id);

            builder.HasIndex(d => d.ChurchId);
            builder.HasIndex(d => d.DeadlineAt);
            builder.HasIndex(d => d.Status);

            builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(d => d.ChallengerDocumentHash).HasMaxLength(128);
            builder.Property(d => d.IncumbentDocumentHash).HasMaxLength(128);
            builder.Property(d => d.ResolutionNotes).HasMaxLength(2000);

            builder.HasOne(d => d.Church)
                   .WithMany(c => c.DisputeCases)
                   .HasForeignKey(d => d.ChurchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(d => d.ChallengerUser)
                   .WithMany()
                   .HasForeignKey(d => d.ChallengerUserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(d => d.IncumbentUser)
                   .WithMany()
                   .HasForeignKey(d => d.IncumbentUserId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasQueryFilter(d => d.Church.DeletedAt == null);
        });

        // CLAIM_AUDIT_LOGS table (Append-Only)
        modelBuilder.Entity<ClaimAuditLog>(builder =>
        {
            builder.ToTable("claim_audit_logs");
            builder.HasKey(a => a.Id);

            builder.HasIndex(a => a.ChurchId);
            builder.HasIndex(a => a.UserId);
            builder.HasIndex(a => a.TimestampUtc);
            builder.HasIndex(a => a.RetentionUntil);

            builder.Property(a => a.EventType).HasMaxLength(100).IsRequired();
            builder.Property(a => a.ClientIp).HasMaxLength(45).IsRequired();
            builder.Property(a => a.UserAgent).HasMaxLength(500).IsRequired();
            builder.Property(a => a.VerificationMetadata).HasColumnType("jsonb").IsRequired();

            builder.HasOne(a => a.Church)
                   .WithMany()
                   .HasForeignKey(a => a.ChurchId)
                   .IsRequired(false)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.User)
                   .WithMany()
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.SetNull);
        });

        // USER_PROFILES table
        modelBuilder.Entity<UserProfile>(builder =>
        {
            builder.ToTable("user_profiles");
            builder.HasKey(p => p.Id);

            // 1:1 Unique Index on UserId
            builder.HasIndex(p => p.UserId).IsUnique();

            builder.Property(p => p.Denomination).HasMaxLength(100);
            builder.Property(p => p.WorshipStyle).HasMaxLength(100);
            builder.Property(p => p.PreferredLanguages).HasConversion(stringListConverter, stringListComparer).HasColumnType("jsonb").IsRequired();
            builder.Property(p => p.DefaultRadiusKm).IsRequired().HasDefaultValue(10.0);
            builder.Property(p => p.IsAnonymous).IsRequired().HasDefaultValue(false);

            builder.HasOne(p => p.User)
                   .WithOne(u => u.Profile)
                   .HasForeignKey<UserProfile>(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // LGPD Soft Delete Filter
            builder.HasQueryFilter(p => p.DeletedAt == null && p.User.DeletedAt == null);
        });

        // TAG_CATALOGS table
        modelBuilder.Entity<TagCatalog>(builder =>
        {
            builder.ToTable("tag_catalogs");
            builder.HasKey(t => t.Id);

            builder.HasIndex(t => t.Code).IsUnique();
            builder.HasIndex(t => t.Category);
            builder.HasIndex(t => t.IsActive);

            builder.Property(t => t.Code).HasMaxLength(100).IsRequired();
            builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
            builder.Property(t => t.Category).IsRequired();
            builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
            builder.Property(t => t.IconName).HasMaxLength(100).IsRequired();
            builder.Property(t => t.IsActive).IsRequired().HasDefaultValue(true);
            builder.Property(t => t.DisplayOrder).IsRequired().HasDefaultValue(0);

            // Seed Data: 15 tags oficiais (Acessibilidade, Infraestrutura, Ministérios)
            builder.HasData(
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000001"),
                    Code = "rampa_acesso",
                    Name = "Rampa de Acesso",
                    Category = TagCategory.Accessibility,
                    Description = "Acesso pleno a cadeirantes e pessoas com mobilidade reduzida.",
                    IconName = "accessible",
                    IsActive = true,
                    DisplayOrder = 1,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000002"),
                    Code = "interprete_libras",
                    Name = "Intérprete de LIBRAS",
                    Category = TagCategory.Accessibility,
                    Description = "Inclusão de surdos e deficientes auditivos durante o culto.",
                    IconName = "sign_language",
                    IsActive = true,
                    DisplayOrder = 2,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000003"),
                    Code = "banheiro_acessivel",
                    Name = "Banheiro Adaptado",
                    Category = TagCategory.Accessibility,
                    Description = "Instalações sanitárias projetadas para acessibilidade.",
                    IconName = "wc",
                    IsActive = true,
                    DisplayOrder = 3,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000004"),
                    Code = "elevador_acessivel",
                    Name = "Elevador / Plataforma",
                    Category = TagCategory.Accessibility,
                    Description = "Acesso facilitado entre diferentes andares do templo.",
                    IconName = "elevator",
                    IsActive = true,
                    DisplayOrder = 4,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0001-000000000005"),
                    Code = "audiodescricao",
                    Name = "Recurso de Audiodescrição",
                    Category = TagCategory.Accessibility,
                    Description = "Apoio para pessoas com deficiência visual.",
                    IconName = "hearing",
                    IsActive = true,
                    DisplayOrder = 5,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000001"),
                    Code = "estacionamento_proprio",
                    Name = "Estacionamento Próprio",
                    Category = TagCategory.Infrastructure,
                    Description = "Vagas gratuitas ou privativas para fiéis e visitantes.",
                    IconName = "local_parking",
                    IsActive = true,
                    DisplayOrder = 6,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000002"),
                    Code = "ar_condicionado",
                    Name = "Ambiente Climatizado",
                    Category = TagCategory.Infrastructure,
                    Description = "Templo totalmente equipado com climatização.",
                    IconName = "ac_unit",
                    IsActive = true,
                    DisplayOrder = 7,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000003"),
                    Code = "espaco_kids_bercario",
                    Name = "Espaço Kids & Berçário",
                    Category = TagCategory.Infrastructure,
                    Description = "Sala dedicada a bebês e crianças pequenas com monitoria.",
                    IconName = "child_care",
                    IsActive = true,
                    DisplayOrder = 8,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000004"),
                    Code = "transmissao_online",
                    Name = "Transmissão Ao Vivo",
                    Category = TagCategory.Infrastructure,
                    Description = "Transmissão em tempo real via YouTube, Facebook ou site.",
                    IconName = "live_tv",
                    IsActive = true,
                    DisplayOrder = 9,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0002-000000000005"),
                    Code = "refeitorio_cantina",
                    Name = "Refeitório / Cantina",
                    Category = TagCategory.Infrastructure,
                    Description = "Espaço comunitário para comunhão e alimentação pós-culto.",
                    IconName = "restaurant",
                    IsActive = true,
                    DisplayOrder = 10,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0003-000000000001"),
                    Code = "ministerio_jovens",
                    Name = "Ministério de Jovens",
                    Category = TagCategory.Ministries,
                    Description = "Reuniões, células e eventos focados no público jovem.",
                    IconName = "groups",
                    IsActive = true,
                    DisplayOrder = 11,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0003-000000000002"),
                    Code = "ministerio_infantil",
                    Name = "Ministério Infantil",
                    Category = TagCategory.Ministries,
                    Description = "Culto infantil paralelo e atividades pedagógicas.",
                    IconName = "child_friendly",
                    IsActive = true,
                    DisplayOrder = 12,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0003-000000000003"),
                    Code = "ministerio_casais",
                    Name = "Ministério de Casais",
                    Category = TagCategory.Ministries,
                    Description = "Cursos, encontros e acompanhamento para famílias.",
                    IconName = "favorite",
                    IsActive = true,
                    DisplayOrder = 13,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0003-000000000004"),
                    Code = "escola_biblica",
                    Name = "Escola Bíblica (EBD)",
                    Category = TagCategory.Ministries,
                    Description = "Aulas sistemáticas de estudo das Escrituras aos domingos.",
                    IconName = "menu_book",
                    IsActive = true,
                    DisplayOrder = 14,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new TagCatalog
                {
                    Id = Guid.Parse("00000000-0000-0000-0003-000000000005"),
                    Code = "acao_social",
                    Name = "Ação Social Comunitária",
                    Category = TagCategory.Ministries,
                    Description = "Distribuição de cestas básicas, roupas e amparo a vulneráveis.",
                    IconName = "volunteer_activism",
                    IsActive = true,
                    DisplayOrder = 15,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                }
            );
        });

        // USER_PROFILE_TAGS table (N:N Junction)
        modelBuilder.Entity<UserProfileTag>(builder =>
        {
            builder.ToTable("user_profile_tags");
            builder.HasKey(pt => new { pt.UserProfileId, pt.TagId });

            builder.HasOne(pt => pt.UserProfile)
                   .WithMany(p => p.UserProfileTags)
                   .HasForeignKey(pt => pt.UserProfileId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pt => pt.Tag)
                   .WithMany(t => t.UserProfileTags)
                   .HasForeignKey(pt => pt.TagId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(pt => pt.UserProfile.DeletedAt == null && pt.UserProfile.User.DeletedAt == null);
        });

        // CHURCH_TAGS table (N:N Junction)
        modelBuilder.Entity<ChurchTag>(builder =>
        {
            builder.ToTable("church_tags");
            builder.HasKey(ct => new { ct.ChurchId, ct.TagId });

            builder.HasOne(ct => ct.Church)
                   .WithMany(c => c.ChurchTags)
                   .HasForeignKey(ct => ct.ChurchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ct => ct.Tag)
                   .WithMany(t => t.ChurchTags)
                   .HasForeignKey(ct => ct.TagId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(ct => ct.Church.DeletedAt == null);
        });

        // CHURCH_MEETING_SCHEDULES table
        modelBuilder.Entity<ChurchMeetingSchedule>(builder =>
        {
            builder.ToTable("church_meeting_schedules");
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => s.ChurchId);
            builder.HasIndex(s => s.DayOfWeek);

            builder.Property(s => s.DayOfWeek).IsRequired();
            builder.Property(s => s.StartTime).HasMaxLength(10).IsRequired();
            builder.Property(s => s.Description).HasMaxLength(200).IsRequired();
            builder.Property(s => s.Language).HasMaxLength(10).IsRequired().HasDefaultValue("pt");

            builder.HasOne(s => s.Church)
                   .WithMany(c => c.MeetingSchedules)
                   .HasForeignKey(s => s.ChurchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(s => s.Church.DeletedAt == null);
        });
    }
}
