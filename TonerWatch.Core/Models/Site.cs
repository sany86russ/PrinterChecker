namespace TonerWatch.Core.Models;

/// <summary>
/// Site entity representing a physical location with printers
/// </summary>
public class Site
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? SubnetCidr { get; set; }
    public int? ProfileId { get; set; }
    public string? QuietHours { get; set; } // JSON format for quiet time ranges
    public string Timezone { get; set; } = TimeZoneInfo.Local.Id;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    public virtual ICollection<Credential> Credentials { get; set; } = new List<Credential>();
}

/// <summary>
/// Polling profile for site-specific settings
/// </summary>
public class PollingProfile
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int IntervalMinutes { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 10;
    public bool EnabledSNMP { get; set; } = true;
    public bool EnabledIPP { get; set; } = true;
    public bool EnabledHTTP { get; set; } = false;
    public bool EnabledPJL { get; set; } = false;
    public string? CustomSettings { get; set; } // JSON for additional settings
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}