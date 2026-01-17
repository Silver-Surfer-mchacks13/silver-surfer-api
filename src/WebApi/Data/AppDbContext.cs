using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WebApi.Models;

namespace WebApi.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options) 
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
    public DbSet<TaskSession> TaskSessions { get; set; }
    public DbSet<ClickAgentAction> ClickAgentActions { get; set; }
    public DbSet<WaitAgentAction> WaitAgentActions { get; set; }
    public DbSet<CompleteAgentAction> CompleteAgentActions { get; set; }
    public DbSet<SitePattern> SitePatterns { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimeValues();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        NormalizeDateTimeValues();
        return base.SaveChanges();
    }

    private void NormalizeDateTimeValues()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime) || property.Metadata.ClrType == typeof(DateTime?))
                {
                    if (property.CurrentValue is DateTime dateTime)
                    {
                        if (dateTime.Kind == DateTimeKind.Unspecified)
                        {
                            // Treat Unspecified as UTC (assume incoming dates are already in UTC)
                            property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                        }
                        else if (dateTime.Kind == DateTimeKind.Local)
                        {
                            // Convert Local to UTC
                            property.CurrentValue = dateTime.ToUniversalTime();
                        }
                        // Already UTC, no change needed
                    }
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // sets the default schema for PostgreSQL
        // re-set when actually using the template
        modelBuilder.HasDefaultSchema("silver_surfers_main");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => new { e.Provider, e.Email }).IsUnique();
            
            entity.HasIndex(e => new { e.Provider, e.ProviderUserId })
                .IsUnique()
                .HasFilter("\"ProviderUserId\" IS NOT NULL");
            
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash)
                .IsRequired(false)
                .HasMaxLength(255); 
            entity.Property(e => e.Provider).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ProviderUserId).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(e => e.RefreshTokens)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(rt => rt.ReplacedByToken)
                .IsRequired(false)
                .HasMaxLength(255);
            entity.Property(rt => rt.RevocationReason)
                .IsRequired(false)
                .HasMaxLength(255);
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.PasswordResetRequests)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<TaskSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Optional relationship to User (nullable FK for future authorization)
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Set null if user is deleted
            
            // Relationships to action tables are configured in their respective entity configurations
        });
        
        // ClickAgentAction configuration
        modelBuilder.Entity<ClickAgentAction>(entity =>
        {
            entity.ToTable("ClickAgentActions", "silver_surfers_main");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Target).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Reasoning).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.PageUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.PageHtml).HasMaxLength(50000);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Session)
                .WithMany(e => e.ClickActions)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WaitAgentAction configuration
        modelBuilder.Entity<WaitAgentAction>(entity =>
        {
            entity.ToTable("WaitAgentActions", "silver_surfers_main");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Duration).IsRequired();
            entity.Property(e => e.Reasoning).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.PageUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.PageHtml).HasMaxLength(50000);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Session)
                .WithMany(e => e.WaitActions)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CompleteAgentAction configuration
        modelBuilder.Entity<CompleteAgentAction>(entity =>
        {
            entity.ToTable("CompleteAgentActions", "silver_surfers_main");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Reasoning).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.PageUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.PageHtml).HasMaxLength(50000);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Session)
                .WithMany(e => e.CompleteActions)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<SitePattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Domain).IsUnique();
            
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PatternJson).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}

