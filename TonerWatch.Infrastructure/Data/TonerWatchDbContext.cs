using Microsoft.EntityFrameworkCore;
using TonerWatch.Core.Models;
using TonerWatch.Core.Interfaces;

namespace TonerWatch.Infrastructure.Data;

/// <summary>
/// Main DbContext for TonerWatch application
/// </summary>
public class TonerWatchDbContext : DbContext
{
    public TonerWatchDbContext(DbContextOptions<TonerWatchDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<Site> Sites { get; set; } = null!;
    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<Supply> Supplies { get; set; } = null!;
    public DbSet<Counter> Counters { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Credential> Credentials { get; set; } = null!;
    public DbSet<ForecastSnapshot> ForecastSnapshots { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<License> Licenses { get; set; } = null!;
    public DbSet<PollingProfile> PollingProfiles { get; set; } = null!;
    public DbSet<ReportTemplate> ReportTemplates { get; set; } = null!;
    
    // Alert and Notification entities
    public DbSet<AlertRule> AlertRules { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<NotificationRecipient> NotificationRecipients { get; set; } = null!;
    public DbSet<NotificationMessage> NotificationMessages { get; set; } = null!;
    public DbSet<NotificationHistoryEntry> NotificationHistory { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Site entity
        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.SubnetCidr).HasMaxLength(50);
            entity.Property(e => e.QuietHours).HasColumnType("TEXT"); // JSON
            entity.Property(e => e.Timezone).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure Device entity
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hostname).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 support
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.Vendor).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(200);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.FirmwareVersion).HasMaxLength(100);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.SystemObjectId).HasMaxLength(200);
            entity.Property(e => e.SystemDescription).HasMaxLength(1000);
            entity.Property(e => e.Tags).HasColumnType("TEXT"); // JSON
            entity.Property(e => e.Notes).HasMaxLength(2000);

            entity.HasIndex(e => e.Hostname);
            entity.HasIndex(e => e.IpAddress);
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => new { e.SiteId, e.Hostname }).IsUnique();

            entity.HasOne(d => d.Site)
                  .WithMany(s => s.Devices)
                  .HasForeignKey(d => d.SiteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Supply entity
        modelBuilder.Entity<Supply>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.PartNumber).HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(7); // #RRGGBB

            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => new { e.DeviceId, e.Kind }).IsUnique();

            entity.HasOne(s => s.Device)
                  .WithMany(d => d.Supplies)
                  .HasForeignKey(s => s.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Counter entity
        modelBuilder.Entity<Counter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => new { e.DeviceId, e.PeriodStart });

            entity.HasOne(c => c.Device)
                  .WithMany(d => d.Counters)
                  .HasForeignKey(c => c.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Event entity
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Details).HasMaxLength(4000);
            entity.Property(e => e.Fingerprint).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            entity.Property(e => e.Resolution).HasMaxLength(2000);

            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Fingerprint);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.DeviceId, e.CreatedAt });

            entity.HasOne(ev => ev.Device)
                  .WithMany(d => d.Events)
                  .HasForeignKey(ev => ev.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Credential entity
        modelBuilder.Entity<Credential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SecretRef).IsRequired().HasMaxLength(500);

            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.DeviceId);

            entity.HasOne(c => c.Site)
                  .WithMany(s => s.Credentials)
                  .HasForeignKey(c => c.SiteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Device)
                  .WithMany(d => d.Credentials)
                  .HasForeignKey(c => c.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ForecastSnapshot entity
        modelBuilder.Entity<ForecastSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Model).HasMaxLength(50);
            entity.Property(e => e.Parameters).HasColumnType("TEXT"); // JSON

            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => new { e.DeviceId, e.SupplyKind, e.At });

            entity.HasOne(f => f.Device)
                  .WithMany(d => d.ForecastSnapshots)
                  .HasForeignKey(f => f.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.ExternalId).HasMaxLength(200);
            entity.Property(e => e.Department).HasMaxLength(200);
            entity.Property(e => e.Preferences).HasColumnType("TEXT"); // JSON

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.ExternalId).IsUnique();
        });

        // Configure License entity
        modelBuilder.Entity<License>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Fingerprint).HasMaxLength(200);
            entity.Property(e => e.LicenseKey).HasMaxLength(1000);
            entity.Property(e => e.CustomerInfo).HasColumnType("TEXT"); // JSON
        });

        // Configure PollingProfile entity
        modelBuilder.Entity<PollingProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CustomSettings).HasColumnType("TEXT"); // JSON

            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure ReportTemplate entity
        modelBuilder.Entity<ReportTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Configuration).HasColumnType("TEXT"); // JSON
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Format);
            entity.HasIndex(e => e.SiteId);
            
            entity.HasOne(rt => rt.Site)
                  .WithMany()
                  .HasForeignKey(rt => rt.SiteId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure enum conversions
        modelBuilder.Entity<Supply>()
            .Property(e => e.Kind)
            .HasConversion<int>();

        modelBuilder.Entity<Device>()
            .Property(e => e.Status)
            .HasConversion<int>();

        modelBuilder.Entity<Device>()
            .Property(e => e.Capabilities)
            .HasConversion<long>();

        modelBuilder.Entity<Event>()
            .Property(e => e.Severity)
            .HasConversion<int>();

        modelBuilder.Entity<Event>()
            .Property(e => e.SupplyKind)
            .HasConversion<int?>();

        modelBuilder.Entity<Credential>()
            .Property(e => e.Type)
            .HasConversion<int>();

        modelBuilder.Entity<Credential>()
            .Property(e => e.Scope)
            .HasConversion<int>();

        modelBuilder.Entity<ForecastSnapshot>()
            .Property(e => e.SupplyKind)
            .HasConversion<int>();

        modelBuilder.Entity<User>()
            .Property(e => e.Role)
            .HasConversion<int>();

        modelBuilder.Entity<License>()
            .Property(e => e.CurrentTier)
            .HasConversion<int>();

        // Configure AlertRule decimal properties
        modelBuilder.Entity<AlertRule>()
            .Property(e => e.WarningThreshold)
            .HasColumnType("DECIMAL(5,2)");

        modelBuilder.Entity<AlertRule>()
            .Property(e => e.CriticalThreshold)
            .HasColumnType("DECIMAL(5,2)");

        modelBuilder.Entity<AlertRule>()
            .Property(e => e.HysteresisMargin)
            .HasColumnType("DECIMAL(5,2)");

        // Configure AlertRule entity
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.DefaultSeverity).HasConversion<int>();
            entity.Property(e => e.SupplyKind).HasConversion<int?>();
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SiteId);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.SupplyKind);
        });

        // Configure Alert entity
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AlertKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Severity).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Metadata).HasColumnType("TEXT"); // JSON
            entity.Property(e => e.SupplyKind).HasConversion<int?>();
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            entity.Property(e => e.ResolvedBy).HasMaxLength(200);
            
            entity.HasIndex(e => e.AlertKey);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.DeviceId, e.Status });
            
            entity.HasOne(a => a.Rule)
                  .WithMany()
                  .HasForeignKey(a => a.RuleId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(a => a.Device)
                  .WithMany()
                  .HasForeignKey(a => a.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure NotificationTemplate entity
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Channel).HasConversion<int>();
            entity.Property(e => e.AlertCategory).HasConversion<int?>();
            entity.Property(e => e.MinSeverity).HasConversion<int?>();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.BodyTemplate).HasColumnType("TEXT");
            entity.Property(e => e.DefaultParameters).HasColumnType("TEXT"); // JSON
            
            entity.HasIndex(e => e.Channel);
            entity.HasIndex(e => e.Name);
        });

        // Configure NotificationRecipient entity
        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Channel).HasConversion<int>();
            entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MinSeverity).HasConversion<int?>();
            entity.Property(e => e.Categories).HasColumnType("TEXT"); // JSON
            entity.Property(e => e.ActiveDays).HasColumnType("TEXT"); // JSON
            
            entity.HasIndex(e => e.Channel);
            entity.HasIndex(e => e.SiteId);
        });

        // Configure NotificationMessage entity
        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Channel).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).HasColumnType("TEXT");
            entity.Property(e => e.Metadata).HasColumnType("TEXT"); // JSON
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            
            entity.HasIndex(e => e.AlertId);
            entity.HasIndex(e => e.RecipientId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(n => n.Alert)
                  .WithMany()
                  .HasForeignKey(n => n.AlertId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(n => n.Recipient)
                  .WithMany()
                  .HasForeignKey(n => n.RecipientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure NotificationHistoryEntry entity
        modelBuilder.Entity<NotificationHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Severity).HasConversion<int>();
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.RecipientInfo).HasMaxLength(1000);
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(200);
            
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.DeviceId, e.CreatedAt });
            
            entity.HasOne(n => n.Device)
                  .WithMany()
                  .HasForeignKey(n => n.DeviceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Save changes with automatic timestamp updates
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is Device device)
            {
                if (entry.State == EntityState.Added)
                    device.CreatedAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is Site site)
            {
                if (entry.State == EntityState.Added)
                    site.CreatedAt = DateTime.UtcNow;
                site.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is Supply supply)
            {
                if (entry.State == EntityState.Added)
                    supply.CreatedAt = DateTime.UtcNow;
                supply.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is Credential credential)
            {
                if (entry.State == EntityState.Added)
                    credential.CreatedAt = DateTime.UtcNow;
                credential.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is User user)
            {
                if (entry.State == EntityState.Added)
                    user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is License license)
            {
                if (entry.State == EntityState.Added)
                    license.CreatedAt = DateTime.UtcNow;
                license.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is PollingProfile profile)
            {
                if (entry.State == EntityState.Added)
                    profile.CreatedAt = DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.Entity is NotificationHistoryEntry history)
            {
                if (entry.State == EntityState.Added)
                    history.CreatedAt = DateTime.UtcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}