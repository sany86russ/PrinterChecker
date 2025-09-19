using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3,
    Emergency = 4
}

/// <summary>
/// Alert status
/// </summary>
public enum AlertStatus
{
    Active = 1,
    Acknowledged = 2,
    Resolved = 3,
    Suppressed = 4
}

/// <summary>
/// Alert categories
/// </summary>
public enum AlertCategory
{
    SupplyLow = 1,
    SupplyCritical = 2,
    DeviceOffline = 3,
    DeviceError = 4,
    ForecastWarning = 5,
    MaintenanceRequired = 6
}

/// <summary>
/// Alert configuration model
/// </summary>
public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AlertCategory Category { get; set; }
    public AlertSeverity DefaultSeverity { get; set; }
    public bool IsEnabled { get; set; } = true;
    
    // Threshold configuration
    public double? WarningThreshold { get; set; }
    public double? CriticalThreshold { get; set; }
    
    // Hysteresis configuration to prevent flapping
    public double? HysteresisMargin { get; set; } = 2.0;
    
    // Deduplication settings
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(15);
    public int MaxOccurrencesPerWindow { get; set; } = 3;
    
    // Site and device filters
    public int? SiteId { get; set; }
    public int? DeviceId { get; set; }
    public SupplyKind? SupplyKind { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Alert instance
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public string AlertKey { get; set; } = string.Empty; // Unique key for deduplication
    public AlertRule Rule { get; set; } = null!;
    public int RuleId { get; set; }
    
    public Device Device { get; set; } = null!;
    public int DeviceId { get; set; }
    
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Active;
    public AlertCategory Category { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Supply-specific data
    public SupplyKind? SupplyKind { get; set; }
    public double? CurrentLevel { get; set; }
    public double? ThresholdValue { get; set; }
    
    // Timestamps
    public DateTime FirstOccurrence { get; set; } = DateTime.UtcNow;
    public DateTime LastOccurrence { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Occurrence tracking
    public int OccurrenceCount { get; set; } = 1;
    public string? AcknowledgedBy { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>
/// Alert evaluation context
/// </summary>
public class AlertContext
{
    public Device Device { get; set; } = null!;
    public Supply? Supply { get; set; }
    public double? CurrentValue { get; set; }
    public double? PreviousValue { get; set; }
    public ForecastResult? Forecast { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Service interface for alert management
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Evaluate alerts for a device
    /// </summary>
    Task<IEnumerable<Alert>> EvaluateDeviceAlertsAsync(int deviceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evaluate alerts for specific supply
    /// </summary>
    Task<Alert?> EvaluateSupplyAlertAsync(AlertContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process batch of alert contexts
    /// </summary>
    Task<IEnumerable<Alert>> ProcessAlertBatchAsync(IEnumerable<AlertContext> contexts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Acknowledge alert
    /// </summary>
    Task<bool> AcknowledgeAlertAsync(int alertId, string acknowledgedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolve alert
    /// </summary>
    Task<bool> ResolveAlertAsync(int alertId, string resolvedBy, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get active alerts with filtering
    /// </summary>
    Task<IEnumerable<Alert>> GetActiveAlertsAsync(int? siteId = null, int? deviceId = null, AlertSeverity? minSeverity = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get alert rules for device/site
    /// </summary>
    Task<IEnumerable<AlertRule>> GetAlertRulesAsync(int? siteId = null, int? deviceId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create or update alert rule
    /// </summary>
    Task<AlertRule> SaveAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete alert rule
    /// </summary>
    Task<bool> DeleteAlertRuleAsync(int ruleId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create or update device-specific alert rule
    /// </summary>
    Task<AlertRule> SaveDeviceSpecificRuleAsync(int deviceId, SupplyKind supplyKind, double warningThreshold, double criticalThreshold, CancellationToken cancellationToken = default);
}