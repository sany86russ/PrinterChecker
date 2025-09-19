namespace TonerWatch.Core.Models;

/// <summary>
/// Event severity levels
/// </summary>
public enum EventSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
    Error = 3
}

/// <summary>
/// Event entity for alerts and notifications
/// </summary>
public class Event
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public SupplyKind? SupplyKind { get; set; }
    public double? LevelPercent { get; set; }
    public int? DaysLeft { get; set; }
    public EventSeverity Severity { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }
    public required string Fingerprint { get; set; } // For deduplication
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public bool IsMuted { get; set; } = false;
    public DateTime? MutedUntil { get; set; }

    // Navigation properties
    public virtual Device Device { get; set; } = null!;

    /// <summary>
    /// Check if event is currently active (not acknowledged or resolved)
    /// </summary>
    public bool IsActive => AcknowledgedAt == null && ResolvedAt == null && !IsCurrentlyMuted;

    /// <summary>
    /// Check if event is currently muted
    /// </summary>
    public bool IsCurrentlyMuted => IsMuted && (MutedUntil == null || MutedUntil > DateTime.UtcNow);

    /// <summary>
    /// Generate fingerprint for deduplication
    /// </summary>
    public static string GenerateFingerprint(int deviceId, SupplyKind? supplyKind, EventSeverity severity)
    {
        var fingerprint = $"{deviceId}:{supplyKind?.ToString() ?? "DEVICE"}:{severity}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fingerprint));
    }

    /// <summary>
    /// Acknowledge the event
    /// </summary>
    public void Acknowledge(string acknowledgedBy)
    {
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resolve the event
    /// </summary>
    public void Resolve(string? resolution = null)
    {
        Resolution = resolution;
        ResolvedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Mute the event for specified duration
    /// </summary>
    public void Mute(TimeSpan duration)
    {
        IsMuted = true;
        MutedUntil = DateTime.UtcNow.Add(duration);
    }
}