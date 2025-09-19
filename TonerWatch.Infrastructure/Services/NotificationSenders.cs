using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using TonerWatch.Core.Interfaces;

namespace TonerWatch.Infrastructure.Services;

public class EmailNotificationSender : IEmailNotificationSender
{
    private readonly ILogger<EmailNotificationSender> _logger;
    private readonly NotificationConfig _config;

    public EmailNotificationSender(ILogger<EmailNotificationSender> logger)
    {
        _logger = logger;
        // Initialize with default configuration - can be updated later
        _config = new NotificationConfig
        {
            SmtpHost = "localhost",
            SmtpPort = 587,
            SmtpUseSsl = true,
            SmtpUsername = "admin@tonerwatch.local",
            SmtpPassword = "password",
            FromEmail = "admin@tonerwatch.local",
            FromName = "TonerWatch System"
        };
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.SmtpHost))
            {
                _logger.LogWarning("SMTP configuration is not set - email notification skipped");
                return false;
            }

            // For demo purposes, just log the email that would be sent
            _logger.LogInformation("[EMAIL SIMULATION] To: {To}, Subject: {Subject}", to, subject);
            _logger.LogDebug("[EMAIL BODY] {Body}", body);
            
            // Simulate email sending delay
            await Task.Delay(100, cancellationToken);
            
            _logger.LogInformation("Email simulation completed successfully to {Recipient}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", to);
            return false;
        }
    }
}

public class TelegramNotificationSender : ITelegramNotificationSender
{
    private readonly ILogger<TelegramNotificationSender> _logger;
    private readonly HttpClient _httpClient;
    private readonly NotificationConfig _config;

    public TelegramNotificationSender(ILogger<TelegramNotificationSender> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        // Initialize with default configuration
        _config = new NotificationConfig
        {
            TelegramBotToken = "demo_bot_token"
        };
    }

    public async Task<bool> SendTelegramMessageAsync(long chatId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.TelegramBotToken) || _config.TelegramBotToken == "demo_bot_token")
            {
                _logger.LogWarning("Telegram bot token is not configured - telegram notification skipped");
                // For demo purposes, just log the message
                _logger.LogInformation("[TELEGRAM SIMULATION] To Chat: {ChatId}, Message: {Message}", chatId, message);
                return true;
            }

            var url = $"https://api.telegram.org/bot{_config.TelegramBotToken}/sendMessage";
            
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram message sent successfully to chat {ChatId}", chatId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Telegram message. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to chat {ChatId}", chatId);
            return false;
        }
    }
}

public class WebhookNotificationSender : IWebhookNotificationSender
{
    private readonly ILogger<WebhookNotificationSender> _logger;
    private readonly HttpClient _httpClient;

    public WebhookNotificationSender(ILogger<WebhookNotificationSender> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<bool> SendWebhookAsync(string url, object payload, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add custom headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    content.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook sent successfully to {Url}", url);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send webhook. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook to {Url}", url);
            return false;
        }
    }
}