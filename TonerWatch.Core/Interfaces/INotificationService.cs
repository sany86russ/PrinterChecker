using TonerWatch.Core.Interfaces;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Notification channel types
/// </summary>
public enum NotificationChannel
{
    Email = 1,
    Telegram = 2,
    Webhook = 3,
    SMS = 4,
    Slack = 5
}

/// <summary>
/// Notification priority
/// </summary>
public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

/// <summary>
/// Notification template
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public AlertCategory? AlertCategory { get; set; }
    public AlertSeverity? MinSeverity { get; set; }
    
    public string Subject { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
    
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification recipient
/// </summary>
public class NotificationRecipient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Address { get; set; } = string.Empty; // Email, phone, chat ID, etc.
    
    // Filtering
    public int? SiteId { get; set; }
    public AlertSeverity? MinSeverity { get; set; }
    public List<AlertCategory> Categories { get; set; } = new();
    
    // Scheduling
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
    public List<DayOfWeek> ActiveDays { get; set; } = new();
    
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Notification configuration
/// </summary>
public class NotificationConfig
{
    // Email settings
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    
    // Telegram settings
    public string? TelegramBotToken { get; set; }
    public long? TelegramDefaultChatId { get; set; }
    
    // Webhook settings
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public Dictionary<string, string> WebhookHeaders { get; set; } = new();
    
    // Rate limiting
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxNotificationsPerWindow { get; set; } = 10;
    
    // Retry settings
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Notification message
/// </summary>
public class NotificationMessage
{
    public int Id { get; set; }
    public Alert Alert { get; set; } = null!;
    public int AlertId { get; set; }
    
    public NotificationRecipient Recipient { get; set; } = null!;
    public int RecipientId { get; set; }
    
    public NotificationChannel Channel { get; set; }
    public NotificationPriority Priority { get; set; }
    
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Status tracking
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Notification status
/// </summary>
public enum NotificationStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5,
    Skipped = 6
}

/// <summary>
/// Service interface for notification management
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send notification for alert
    /// </summary>
    Task<IEnumerable<NotificationMessage>> SendAlertNotificationAsync(Alert alert, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send notification to specific recipient
    /// </summary>
    Task<NotificationMessage> SendNotificationAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process pending notifications
    /// </summary>
    Task ProcessPendingNotificationsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get notification recipients for alert
    /// </summary>
    Task<IEnumerable<NotificationRecipient>> GetRecipientsForAlertAsync(Alert alert, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Test notification channel
    /// </summary>
    Task<bool> TestNotificationChannelAsync(NotificationChannel channel, string address, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get notification templates
    /// </summary>
    Task<IEnumerable<NotificationTemplate>> GetNotificationTemplatesAsync(NotificationChannel? channel = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save notification configuration
    /// </summary>
    Task SaveNotificationConfigAsync(NotificationConfig config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get notification configuration
    /// </summary>
    Task<NotificationConfig> GetNotificationConfigAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Channel-specific notification sender interfaces
/// </summary>
public interface IEmailNotificationSender
{
    Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public interface ITelegramNotificationSender
{
    Task<bool> SendTelegramMessageAsync(long chatId, string message, CancellationToken cancellationToken = default);
}

public interface IWebhookNotificationSender
{
    Task<bool> SendWebhookAsync(string url, object payload, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}