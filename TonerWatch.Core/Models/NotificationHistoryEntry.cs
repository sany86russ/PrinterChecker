using System.ComponentModel.DataAnnotations.Schema;
using TonerWatch.Core.Interfaces;

namespace TonerWatch.Core.Models;

/// <summary>
/// Notification history entry for tracking sent notifications
/// </summary>
public class NotificationHistoryEntry
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public EventSeverity Severity { get; set; }
    public AlertCategory Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RecipientInfo { get; set; } // JSON or simple string with recipient details
    public bool IsAcknowledged { get; set; } = false;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }

    // Navigation properties
    public virtual Device Device { get; set; } = null!;
}