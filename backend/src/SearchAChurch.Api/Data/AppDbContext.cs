using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

            builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
            builder.Property(c => c.FormattedAddress).HasMaxLength(500).IsRequired();
            builder.Property(c => c.PlaceId).HasMaxLength(255);
            builder.Property(c => c.Phone).HasMaxLength(50);
            builder.Property(c => c.Website).HasMaxLength(500);
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
    }
}
