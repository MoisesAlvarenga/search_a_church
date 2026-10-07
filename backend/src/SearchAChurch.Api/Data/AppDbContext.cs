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
    }
}
