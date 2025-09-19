namespace TonerWatch.Core.Configuration;

/// <summary>
/// Application configuration options
/// </summary>
public class TonerWatchOptions
{
    public const string SectionName = "TonerWatch";

    public DatabaseOptions Database { get; set; } = new();
    public PollingOptions Polling { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public LicensingOptions Licensing { get; set; } = new();
    public NotificationOptions Notifications { get; set; } = new();
    public DiscoveryOptions Discovery { get; set; } = new();
    public ForecastOptions Forecast { get; set; } = new();
    public AlertOptions Alerts { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
}

/// <summary>
/// Database configuration options
/// </summary>
public class DatabaseOptions
{
    public string Provider { get; set; } = "SQLite"; // SQLite, PostgreSQL
    public string ConnectionString { get; set; } = "Data Source=tonerwatch.db";
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool AutoMigrate { get; set; } = true;
}

/// <summary>
/// Polling configuration options
/// </summary>
public class PollingOptions
{
    public int DefaultIntervalMinutes { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 50;
    public int RetryAttempts { get; set; } = 3;
    public int BackoffMultiplier { get; set; } = 2;
    public bool EnableJitter { get; set; } = true;
    public int JitterMaxSeconds { get; set; } = 300; // 5 minutes
}

/// <summary>
/// Security configuration options
/// </summary>
public class SecurityOptions
{
    public string SecretProvider { get; set; } = "DPAPI"; // DPAPI, CredentialManager, KeyVault
    public string EncryptionScope { get; set; } = "CurrentUser"; // CurrentUser, LocalMachine
    public bool RequireHttps { get; set; } = true;
    public bool EnableApiKeyAuth { get; set; } = false;
    public string? ApiKeyHeader { get; set; } = "X-API-Key";
    public AuthenticationOptions Authentication { get; set; } = new();
}

/// <summary>
/// Authentication configuration options
/// </summary>
public class AuthenticationOptions
{
    public bool EnableWindowsAuth { get; set; } = true;
    public bool EnableOpenIdConnect { get; set; } = false;
    public string? OpenIdConnectAuthority { get; set; }
    public string? OpenIdConnectClientId { get; set; }
    public string? OpenIdConnectClientSecret { get; set; }
    public bool EnableActiveDirectoryGroups { get; set; } = false;
    public Dictionary<string, string> RoleMapping { get; set; } = new();
}

/// <summary>
/// Licensing configuration options
/// </summary>
public class LicensingOptions
{
    public string LicenseFilePath { get; set; } = "license.json";
    public string? LicenseServerUrl { get; set; }
    public int ValidationIntervalHours { get; set; } = 24;
    public int GracePeriodDays { get; set; } = 7;
    public bool EnableTelemetry { get; set; } = false;
    public string? CustomerId { get; set; }
}

/// <summary>
/// Notification configuration options
/// </summary>
public class NotificationOptions
{
    public EmailOptions Email { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
    public WebhookOptions Webhook { get; set; } = new();
    public bool EnableDailyDigest { get; set; } = true;
    public TimeSpan DigestTime { get; set; } = TimeSpan.FromHours(8); // 8 AM
}

/// <summary>
/// Email notification options
/// </summary>
public class EmailOptions
{
    public bool Enabled { get; set; } = false;
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; } = "TonerWatch";
    public List<string> DefaultRecipients { get; set; } = new();
}

/// <summary>
/// Telegram notification options
/// </summary>
public class TelegramOptions
{
    public bool Enabled { get; set; } = false;
    public string? BotToken { get; set; }
    public List<string> DefaultChannels { get; set; } = new();
}

/// <summary>
/// Webhook notification options
/// </summary>
public class WebhookOptions
{
    public bool Enabled { get; set; } = false;
    public List<WebhookEndpoint> Endpoints { get; set; } = new();
}

/// <summary>
/// Webhook endpoint configuration
/// </summary>
public class WebhookEndpoint
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public string? Secret { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
}

/// <summary>
/// Discovery configuration options
/// </summary>
public class DiscoveryOptions
{
    public bool EnableActiveDirectory { get; set; } = true;
    public bool EnablePrintServers { get; set; } = true;
    public bool EnableSubnetScan { get; set; } = true;
    public bool EnableMulticast { get; set; } = false;
    public List<string> SubnetRanges { get; set; } = new();
    public List<string> ExcludeRanges { get; set; } = new();
    public int ScanTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 50;
}

/// <summary>
/// Forecast configuration options
/// </summary>
public class ForecastOptions
{
    public double EwmaAlpha { get; set; } = 0.3;
    public int MinimumDataPoints { get; set; } = 7;
    public int HistoryDays { get; set; } = 30;
    public bool ConsiderWeeklySeasonality { get; set; } = true;
    public double ConfidenceLevel { get; set; } = 0.8;
    public int MaxDaysToPredict { get; set; } = 365;
    public int UpdateIntervalHours { get; set; } = 6;
}

/// <summary>
/// Alert configuration options
/// </summary>
public class AlertOptions
{
    public double CriticalThreshold { get; set; } = 15.0;
    public double WarningThreshold { get; set; } = 30.0;
    public int CriticalDaysLeft { get; set; } = 3;
    public int WarningDaysLeft { get; set; } = 7;
    public double HysteresisMargin { get; set; } = 5.0;
    public TimeSpan DeduplicationTtl { get; set; } = TimeSpan.FromHours(24);
    public bool EnableQuietHours { get; set; } = false;
    public TimeSpan QuietStart { get; set; } = TimeSpan.FromHours(18); // 6 PM
    public TimeSpan QuietEnd { get; set; } = TimeSpan.FromHours(8); // 8 AM
}

/// <summary>
/// Logging configuration options
/// </summary>
public class LoggingOptions
{
    public string LogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public string LogPath { get; set; } = "logs";
    public int RetainDays { get; set; } = 30;
    public long FileSizeLimitMB { get; set; } = 10;
    public bool EnableStructuredLogging { get; set; } = true;
    public bool EnablePerformanceLogging { get; set; } = false;
}