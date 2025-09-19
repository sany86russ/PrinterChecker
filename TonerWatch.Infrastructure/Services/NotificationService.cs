using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IEmailNotificationSender _emailSender;
    private readonly ITelegramNotificationSender _telegramSender;
    private readonly IWebhookNotificationSender _webhookSender;
    private readonly INotificationHistoryService _notificationHistoryService;
    
    private NotificationConfig _config = new();
    private readonly List<NotificationRecipient> _recipients = new();
    private readonly List<NotificationTemplate> _templates = new();
    private readonly List<NotificationMessage> _pendingMessages = new();
    private readonly Dictionary<string, List<DateTime>> _rateLimitTracker = new();

    public NotificationService(
        ILogger<NotificationService> logger,
        IEmailNotificationSender emailSender,
        ITelegramNotificationSender telegramSender,
        IWebhookNotificationSender webhookSender,
        INotificationHistoryService notificationHistoryService)
    {
        _logger = logger;
        _emailSender = emailSender;
        _telegramSender = telegramSender;
        _webhookSender = webhookSender;
        _notificationHistoryService = notificationHistoryService;
        
        InitializeDefaultTemplates();
        InitializeDefaultRecipients();
    }

    public async Task<IEnumerable<NotificationMessage>> SendAlertNotificationAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending notifications for alert {AlertId} - {Title}", alert.Id, alert.Title);

        var recipients = await GetRecipientsForAlertAsync(alert, cancellationToken);
        var messages = new List<NotificationMessage>();

        foreach (var recipient in recipients)
        {
            try
            {
                // Check quiet hours
                if (!IsWithinActiveHours(recipient, alert.Device?.Site))
                {
                    _logger.LogDebug("Skipping notification to {Recipient} due to quiet hours", recipient.Name);
                    continue;
                }

                // Check rate limiting
                if (IsRateLimited(recipient))
                {
                    _logger.LogWarning("Rate limit exceeded for recipient {Recipient}", recipient.Name);
                    continue;
                }

                var message = await CreateNotificationMessage(alert, recipient);
                if (message != null)
                {
                    messages.Add(message);
                    _pendingMessages.Add(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notification for recipient {Recipient}", recipient.Name);
            }
        }

        _logger.LogInformation("Created {MessageCount} notification messages for alert {AlertId}", 
            messages.Count, alert.Id);

        return messages;
    }

    public async Task<NotificationMessage> SendNotificationAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending notification {MessageId} via {Channel}", message.Id, message.Channel);

        // Check quiet hours before sending
        if (!IsWithinActiveHours(message.Recipient, message.Alert?.Device?.Site))
        {
            _logger.LogDebug("Skipping notification to {Recipient} due to quiet hours", message.Recipient.Name);
            message.Status = NotificationStatus.Skipped;
            return message;
        }

        message.Status = NotificationStatus.Sending;
        
        try
        {
            bool success = message.Channel switch
            {
                NotificationChannel.Email => await _emailSender.SendEmailAsync(
                    message.Recipient.Address, message.Subject, message.Body, cancellationToken),
                NotificationChannel.Telegram => await _telegramSender.SendTelegramMessageAsync(
                    long.Parse(message.Recipient.Address), message.Body, cancellationToken),
                NotificationChannel.Webhook => await _webhookSender.SendWebhookAsync(
                    message.Recipient.Address, CreateWebhookPayload(message), _config.WebhookHeaders, cancellationToken),
                _ => false
            };

            if (success)
            {
                message.Status = NotificationStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                TrackRateLimit(message.Recipient);
                
                _logger.LogInformation("Successfully sent notification {MessageId} to {Recipient}", 
                    message.Id, message.Recipient.Name);
                
                // Add to notification history
                try
                {
                    await _notificationHistoryService.AddToHistoryAsync(
                        message.Alert.Device,
                        message.Alert.Title,
                        message.Body,
                        (EventSeverity)(int)message.Alert.Severity,
                        message.Alert.Category,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add notification to history");
                }
            }
            else
            {
                await HandleSendFailure(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification {MessageId}", message.Id);
            message.ErrorMessage = ex.Message;
            await HandleSendFailure(message);
        }

        return message;
    }

    public async Task ProcessPendingNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = _pendingMessages
            .Where(m => m.Status == NotificationStatus.Pending || 
                       (m.Status == NotificationStatus.Failed && m.NextRetryAt <= DateTime.UtcNow))
            .ToList();

        _logger.LogDebug("Processing {PendingCount} pending notifications", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await SendNotificationAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification {MessageId}", message.Id);
            }
        }

        // Clean up completed messages
        _pendingMessages.RemoveAll(m => m.Status == NotificationStatus.Sent || 
                                       m.Status == NotificationStatus.Cancelled ||
                                       m.Status == NotificationStatus.Skipped ||
                                       m.RetryCount >= _config.MaxRetries);
    }

    public async Task<IEnumerable<NotificationRecipient>> GetRecipientsForAlertAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        return _recipients.Where(r => r.IsEnabled &&
            (r.SiteId == null || r.SiteId == alert.Device.SiteId) &&
            (r.MinSeverity == null || alert.Severity >= r.MinSeverity) &&
            (r.Categories.Count == 0 || r.Categories.Contains(alert.Category)));
    }

    public async Task<bool> TestNotificationChannelAsync(NotificationChannel channel, string address, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing notification channel {Channel} to {Address}", channel, address);

        try
        {
            var testMessage = "TonerWatch Test Notification\n\nThis is a test message to verify your notification settings are working correctly.\n\nTime: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            bool success = channel switch
            {
                NotificationChannel.Email => await _emailSender.SendEmailAsync(address, "TonerWatch Test", testMessage, cancellationToken),
                NotificationChannel.Telegram => await _telegramSender.SendTelegramMessageAsync(long.Parse(address), testMessage, cancellationToken),
                NotificationChannel.Webhook => await _webhookSender.SendWebhookAsync(address, new { test = true, message = testMessage }, _config.WebhookHeaders, cancellationToken),
                _ => false
            };

            _logger.LogInformation("Test notification {Result} for {Channel}", success ? "succeeded" : "failed", channel);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test notification failed for {Channel}", channel);
            return false;
        }
    }

    public async Task<IEnumerable<NotificationTemplate>> GetNotificationTemplatesAsync(NotificationChannel? channel = null, CancellationToken cancellationToken = default)
    {
        var templates = _templates.AsEnumerable();
        
        if (channel.HasValue)
        {
            templates = templates.Where(t => t.Channel == channel.Value);
        }

        return templates.Where(t => t.IsEnabled);
    }

    public async Task SaveNotificationConfigAsync(NotificationConfig config, CancellationToken cancellationToken = default)
    {
        _config = config;
        _logger.LogInformation("Notification configuration updated");
    }

    public async Task<NotificationConfig> GetNotificationConfigAsync(CancellationToken cancellationToken = default)
    {
        return _config;
    }

    private async Task<NotificationMessage?> CreateNotificationMessage(Alert alert, NotificationRecipient recipient)
    {
        var template = _templates.FirstOrDefault(t => 
            t.IsEnabled && 
            t.Channel == recipient.Channel &&
            (t.AlertCategory == null || t.AlertCategory == alert.Category) &&
            (t.MinSeverity == null || alert.Severity >= t.MinSeverity));

        if (template == null)
        {
            _logger.LogWarning("No suitable template found for alert {AlertId} and recipient {Recipient}", 
                alert.Id, recipient.Name);
            return null;
        }

        var subject = ReplaceTokens(template.Subject, alert);
        var body = ReplaceTokens(template.BodyTemplate, alert);

        return new NotificationMessage
        {
            Id = _pendingMessages.Count + 1,
            Alert = alert,
            AlertId = alert.Id,
            Recipient = recipient,
            RecipientId = recipient.Id,
            Channel = recipient.Channel,
            Priority = GetNotificationPriority(alert.Severity),
            Subject = subject,
            Body = body,
            Metadata = new Dictionary<string, object>
            {
                ["template_id"] = template.Id,
                ["template_name"] = template.Name
            }
        };
    }

    private string ReplaceTokens(string template, Alert alert)
    {
        return template
            .Replace("{{alert.title}}", alert.Title)
            .Replace("{{alert.description}}", alert.Description)
            .Replace("{{alert.severity}}", alert.Severity.ToString())
            .Replace("{{device.name}}", alert.Device.Hostname)
            .Replace("{{device.location}}", alert.Device.Location ?? "Unknown")
            .Replace("{{device.ip}}", alert.Device.IpAddress ?? "Unknown")
            .Replace("{{supply.kind}}", alert.SupplyKind?.ToString() ?? "N/A")
            .Replace("{{supply.level}}", alert.CurrentLevel?.ToString("F1") ?? "N/A")
            .Replace("{{threshold}}", alert.ThresholdValue?.ToString("F1") ?? "N/A")
            .Replace("{{timestamp}}", alert.LastOccurrence.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{{count}}", alert.OccurrenceCount.ToString());
    }

    private NotificationPriority GetNotificationPriority(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Emergency => NotificationPriority.Urgent,
            AlertSeverity.Critical => NotificationPriority.High,
            AlertSeverity.Warning => NotificationPriority.Normal,
            AlertSeverity.Info => NotificationPriority.Low,
            _ => NotificationPriority.Normal
        };
    }

    private bool IsWithinActiveHours(NotificationRecipient recipient, Site? site = null)
    {
        var now = DateTime.Now;
        
        // Check active days
        if (recipient.ActiveDays.Count > 0 && !recipient.ActiveDays.Contains(now.DayOfWeek))
        {
            return false;
        }

        // Check recipient-specific quiet hours
        if (recipient.QuietHoursStart.HasValue && recipient.QuietHoursEnd.HasValue)
        {
            var currentTime = now.TimeOfDay;
            var start = recipient.QuietHoursStart.Value;
            var end = recipient.QuietHoursEnd.Value;

            if (start <= end)
            {
                // Same day range
                return currentTime < start || currentTime > end;
            }
            else
            {
                // Overnight range
                return currentTime < start && currentTime > end;
            }
        }

        // Check site-specific quiet hours if recipient is associated with a site
        if (site != null && !string.IsNullOrEmpty(site.QuietHours))
        {
            try
            {
                var quietHoursConfig = QuietHoursConfiguration.FromJson(site.QuietHours);
                return !quietHoursConfig.IsQuietHours(now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse quiet hours configuration for site {SiteId}", site.Id);
            }
        }

        return true;
    }

    private bool IsRateLimited(NotificationRecipient recipient)
    {
        var key = $"{recipient.Id}_{recipient.Channel}";
        var now = DateTime.UtcNow;
        
        if (!_rateLimitTracker.ContainsKey(key))
        {
            _rateLimitTracker[key] = new List<DateTime>();
        }

        var timestamps = _rateLimitTracker[key];
        
        // Remove old timestamps outside the window
        timestamps.RemoveAll(t => now - t > _config.RateLimitWindow);
        
        return timestamps.Count >= _config.MaxNotificationsPerWindow;
    }

    private void TrackRateLimit(NotificationRecipient recipient)
    {
        var key = $"{recipient.Id}_{recipient.Channel}";
        if (!_rateLimitTracker.ContainsKey(key))
        {
            _rateLimitTracker[key] = new List<DateTime>();
        }
        
        _rateLimitTracker[key].Add(DateTime.UtcNow);
    }

    private async Task HandleSendFailure(NotificationMessage message)
    {
        message.Status = NotificationStatus.Failed;
        message.RetryCount++;

        if (message.RetryCount < _config.MaxRetries)
        {
            message.NextRetryAt = DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(
                _config.RetryDelay.TotalMilliseconds * Math.Pow(2, message.RetryCount - 1)));
            
            _logger.LogWarning("Notification {MessageId} failed, scheduling retry {RetryCount}/{MaxRetries} at {NextRetry}", 
                message.Id, message.RetryCount, _config.MaxRetries, message.NextRetryAt);
        }
        else
        {
            _logger.LogError("Notification {MessageId} permanently failed after {RetryCount} attempts", 
                message.Id, message.RetryCount);
        }
    }

    private object CreateWebhookPayload(NotificationMessage message)
    {
        return new
        {
            alert = new
            {
                id = message.Alert.Id,
                title = message.Alert.Title,
                description = message.Alert.Description,
                severity = message.Alert.Severity.ToString(),
                category = message.Alert.Category.ToString(),
                status = message.Alert.Status.ToString(),
                device = new
                {
                    id = message.Alert.Device.Id,
                    name = message.Alert.Device.Hostname,
                    ip = message.Alert.Device.IpAddress,
                    location = message.Alert.Device.Location
                },
                supply = message.Alert.SupplyKind != null ? new
                {
                    kind = message.Alert.SupplyKind.ToString(),
                    level = message.Alert.CurrentLevel,
                    threshold = message.Alert.ThresholdValue
                } : null,
                timestamps = new
                {
                    first_occurrence = message.Alert.FirstOccurrence,
                    last_occurrence = message.Alert.LastOccurrence
                },
                occurrence_count = message.Alert.OccurrenceCount
            },
            notification = new
            {
                id = message.Id,
                channel = message.Channel.ToString(),
                priority = message.Priority.ToString(),
                created_at = message.CreatedAt
            }
        };
    }

    private void InitializeDefaultTemplates()
    {
        _templates.AddRange(new[]
        {
            new NotificationTemplate
            {
                Id = 1,
                Name = "Email Supply Alert",
                Channel = NotificationChannel.Email,
                AlertCategory = AlertCategory.SupplyLow,
                Subject = "🖨️ TonerWatch Alert: {{alert.title}}",
                BodyTemplate = @"
Уведомление от системы мониторинга принтеров TonerWatch

🔔 АЛЕРТ: {{alert.title}}
📟 Устройство: {{device.name}}
📍 Расположение: {{device.location}}
🌐 IP-адрес: {{device.ip}}

📊 Детали:
• Тип расходника: {{supply.kind}}
• Текущий уровень: {{supply.level}}%
• Пороговое значение: {{threshold}}%
• Серьезность: {{alert.severity}}

📝 Описание: {{alert.description}}

⏰ Время: {{timestamp}}
🔄 Количество повторений: {{count}}

TonerWatch Printer Supply Monitor
"
            },
            new NotificationTemplate
            {
                Id = 2,
                Name = "Telegram Supply Alert",
                Channel = NotificationChannel.Telegram,
                AlertCategory = AlertCategory.SupplyLow,
                BodyTemplate = @"
🖨️ *TonerWatch Alert*

🔔 {{alert.title}}
📟 {{device.name}} ({{device.location}})

📊 {{supply.kind}}: {{supply.level}}% ⚠️
🎯 Порог: {{threshold}}%
⚡ Серьезность: {{alert.severity}}

⏰ {{timestamp}}
"
            },
            new NotificationTemplate
            {
                Id = 3,
                Name = "Device Offline Email",
                Channel = NotificationChannel.Email,
                AlertCategory = AlertCategory.DeviceOffline,
                Subject = "🚨 TonerWatch: Device Offline - {{device.name}}",
                BodyTemplate = @"
Критическое уведомление от TonerWatch

🚨 УСТРОЙСТВО НЕДОСТУПНО

📟 Устройство: {{device.name}}
📍 Расположение: {{device.location}}
🌐 IP-адрес: {{device.ip}}

❌ Устройство не отвечает на SNMP запросы
⏰ Время обнаружения: {{timestamp}}
🔄 Количество проверок: {{count}}

Пожалуйста, проверьте:
• Питание устройства
• Сетевое подключение
• SNMP настройки

TonerWatch System
"
            }
        });

        _logger.LogInformation("Initialized {TemplateCount} notification templates", _templates.Count);
    }

    private void InitializeDefaultRecipients()
    {
        _recipients.AddRange(new[]
        {
            new NotificationRecipient
            {
                Id = 1,
                Name = "IT Administrator Email",
                Channel = NotificationChannel.Email,
                Address = "admin@example.com",
                MinSeverity = AlertSeverity.Warning,
                Categories = new List<AlertCategory> { AlertCategory.SupplyLow, AlertCategory.DeviceOffline },
                ActiveDays = Enum.GetValues<DayOfWeek>().ToList(),
                QuietHoursStart = TimeSpan.FromHours(22),
                QuietHoursEnd = TimeSpan.FromHours(8)
            },
            new NotificationRecipient
            {
                Id = 2,
                Name = "Emergency Telegram",
                Channel = NotificationChannel.Telegram,
                Address = "123456789", // Chat ID
                MinSeverity = AlertSeverity.Critical,
                Categories = new List<AlertCategory> { AlertCategory.DeviceOffline, AlertCategory.SupplyCritical }
            }
        });

        _logger.LogInformation("Initialized {RecipientCount} notification recipients", _recipients.Count);
    }
}