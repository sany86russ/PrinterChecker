using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Service interface for notification history management
/// </summary>
public interface INotificationHistoryService
{
    /// <summary>
    /// Get notification history with filtering options
    /// </summary>
    Task<IEnumerable<NotificationHistoryEntry>> GetNotificationHistoryAsync(
        NotificationHistoryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get notification statistics
    /// </summary>
    Task<NotificationStatistics> GetNotificationStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add notification to history
    /// </summary>
    Task AddToHistoryAsync(
        Device device,
        string title,
        string message,
        EventSeverity severity,
        AlertCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old notification history
    /// </summary>
    Task CleanupHistoryAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter options for notification history
/// </summary>
public class NotificationHistoryFilter
{
    public int? DeviceId { get; set; }
    public int? SiteId { get; set; }
    public EventSeverity? Severity { get; set; }
    public AlertCategory? Category { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Notification statistics
/// </summary>
public class NotificationStatistics
{
    public int TotalCount { get; set; }
    public Dictionary<EventSeverity, int> SeverityCounts { get; set; } = new();
    public Dictionary<AlertCategory, int> CategoryCounts { get; set; } = new();
    public DateRange DateRange { get; set; } = new();
}

/// <summary>
/// Date range
/// </summary>
public class DateRange
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}