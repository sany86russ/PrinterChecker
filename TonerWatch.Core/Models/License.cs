namespace TonerWatch.Core.Models;

/// <summary>
/// License tiers
/// </summary>
public enum LicenseTier
{
    Free = 1,
    Basic = 2,
    Pro = 3,
    Enterprise = 4
}

/// <summary>
/// License entity for managing software licensing
/// </summary>
public class License
{
    public int Id { get; set; }
    public LicenseTier CurrentTier { get; set; } = LicenseTier.Free;
    public int PrintersLimit { get; set; } = 5;
    public int SitesLimit { get; set; } = 1;
    public DateTime? ExpireAt { get; set; }
    public string? Fingerprint { get; set; } // Hardware fingerprint
    public string? LicenseKey { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsTrial { get; set; } = false;
    public DateTime? TrialStarted { get; set; }
    public DateTime? LastValidated { get; set; }
    public string? CustomerInfo { get; set; } // JSON customer information
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if license is currently valid
    /// </summary>
    public bool IsValid => IsActive && (ExpireAt == null || ExpireAt > DateTime.UtcNow);

    /// <summary>
    /// Check if license is expired
    /// </summary>
    public bool IsExpired => ExpireAt.HasValue && ExpireAt <= DateTime.UtcNow;

    /// <summary>
    /// Get days until expiration
    /// </summary>
    public int? DaysUntilExpiration => ExpireAt?.Subtract(DateTime.UtcNow).Days;

    /// <summary>
    /// Check if feature is available for current license tier
    /// </summary>
    public bool HasFeature(string feature)
    {
        return feature switch
        {
            "basic_alerts" => CurrentTier >= LicenseTier.Free,
            "email_notifications" => CurrentTier >= LicenseTier.Free,
            "forecast" => CurrentTier >= LicenseTier.Basic,
            "telegram_notifications" => CurrentTier >= LicenseTier.Basic,
            "teams_notifications" => CurrentTier >= LicenseTier.Basic,
            "multiple_sites" => CurrentTier >= LicenseTier.Basic,
            "site_collectors" => CurrentTier >= LicenseTier.Pro,
            "snmpv3" => CurrentTier >= LicenseTier.Pro,
            "rbac" => CurrentTier >= LicenseTier.Pro,
            "prometheus_export" => CurrentTier >= LicenseTier.Pro,
            "webhooks" => CurrentTier >= LicenseTier.Pro,
            "powershell_hooks" => CurrentTier >= LicenseTier.Pro,
            "sso" => CurrentTier >= LicenseTier.Enterprise,
            "ad_groups" => CurrentTier >= LicenseTier.Enterprise,
            "ha_postgres" => CurrentTier >= LicenseTier.Enterprise,
            _ => false
        };
    }

    /// <summary>
    /// Get feature limits for current tier
    /// </summary>
    public Dictionary<string, int> GetLimits()
    {
        return CurrentTier switch
        {
            LicenseTier.Free => new Dictionary<string, int>
            {
                ["printers"] = 5,
                ["sites"] = 1,
                ["users"] = 1,
                ["retention_days"] = 30
            },
            LicenseTier.Basic => new Dictionary<string, int>
            {
                ["printers"] = 50,
                ["sites"] = 2,
                ["users"] = 5,
                ["retention_days"] = 90
            },
            LicenseTier.Pro => new Dictionary<string, int>
            {
                ["printers"] = 500,
                ["sites"] = int.MaxValue,
                ["users"] = 25,
                ["retention_days"] = 365
            },
            LicenseTier.Enterprise => new Dictionary<string, int>
            {
                ["printers"] = int.MaxValue,
                ["sites"] = int.MaxValue,
                ["users"] = int.MaxValue,
                ["retention_days"] = int.MaxValue
            },
            _ => new Dictionary<string, int>()
        };
    }
}