using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Configuration;
using TonerWatch.Core.Models;

namespace TonerWatch.Desktop.Services;

/// <summary>
/// Менеджер настроек для Desktop приложения
/// </summary>
public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly string _settingsPath;
    private DesktopSettings _settings;

    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _logger = logger;
        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TonerWatch", "settings.json");
        _settings = LoadSettings();
    }

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

    public DesktopSettings Settings => _settings;

    public void AddDevice(DeviceSettings device)
    {
        try
        {
            var oldSettings = _settings;
            _settings.Devices.Add(device);
            SaveSettings();
            
            _logger.LogInformation("Устройство {DeviceName} добавлено", device.Name);
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(oldSettings, _settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка добавления устройства {DeviceName}", device.Name);
            throw;
        }
    }

    public void RemoveDevice(string hostnameOrIp)
    {
        try
        {
            var oldSettings = _settings;
            var device = _settings.Devices.FirstOrDefault(d => d.HostnameOrIp.Equals(hostnameOrIp, StringComparison.OrdinalIgnoreCase));
            if (device != null)
            {
                _settings.Devices.Remove(device);
                SaveSettings();
                
                _logger.LogInformation("Устройство {DeviceName} удалено", device.Name);
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(oldSettings, _settings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления устройства с IP {HostnameOrIp}", hostnameOrIp);
            throw;
        }
    }

    public void UpdateSettings(DesktopSettings newSettings)
    {
        try
        {
            var oldSettings = _settings;
            _settings = newSettings;
            SaveSettings();
            
            _logger.LogInformation("Настройки успешно обновлены");
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(oldSettings, newSettings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления настроек");
            throw;
        }
    }

    public void ResetToDefaults()
    {
        try
        {
            var oldSettings = _settings;
            _settings = new DesktopSettings();
            SaveSettings();
            
            _logger.LogInformation("Настройки сброшены к значениям по умолчанию");
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(oldSettings, _settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сброса настроек");
            throw;
        }
    }

    private DesktopSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("Файл настроек не найден, создаём настройки по умолчанию");
                var defaultSettings = new DesktopSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<DesktopSettings>(json);
            
            if (settings == null)
            {
                _logger.LogWarning("Не удалось загрузить настройки, используем значения по умолчанию");
                return new DesktopSettings();
            }

            _logger.LogInformation("Настройки успешно загружены");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки настроек, используем значения по умолчанию");
            return new DesktopSettings();
        }
    }

    private void SaveSettings(DesktopSettings? settings = null)
    {
        try
        {
            var settingsToSave = settings ?? _settings;
            var directory = Path.GetDirectoryName(_settingsPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(settingsToSave, options);
            File.WriteAllText(_settingsPath, json);
            
            _logger.LogDebug("Настройки сохранены в {SettingsPath}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения настроек");
            throw;
        }
    }
}

/// <summary>
/// Модель настроек Desktop приложения
/// </summary>
public class DesktopSettings
{
    // Общие настройки
    public int RefreshIntervalSeconds { get; set; } = 60;
    public int DiscoveryIntervalMinutes { get; set; } = 30;
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowDesktopNotifications { get; set; } = true;
    public string Language { get; set; } = "ru";

    // Пороги уведомлений
    public double CriticalThreshold { get; set; } = 10.0;
    public double WarningThreshold { get; set; } = 25.0;
    public bool WindowsNotificationsEnabled { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = false;

    // Сетевые настройки
    public string SubnetRange { get; set; } = "192.168.0.0/24";
    public bool AutoDiscoveryEnabled { get; set; } = true;
    public bool SnmpEnabled { get; set; } = true;
    public int MaxConcurrentScans { get; set; } = 50; // Maximum concurrent network scans
    public int ScanTimeoutSeconds { get; set; } = 3; // Timeout for each scan in seconds
    public int ScanRetries { get; set; } = 1; // Number of retries for failed scans
    public int ScanRetryDelayMs { get; set; } = 1000; // Delay between retries in milliseconds
    
    // Network segments for multi-segment support
    public List<NetworkSegment> NetworkSegments { get; set; } = new();
    
    // Global scheduling settings
    public bool EnableScheduledScanning { get; set; } = false;
    public string ScanSchedule { get; set; } = "0 0 * * *"; // Default: every hour
    public bool AvoidPeakTimes { get; set; } = false; // Avoid scanning during peak hours (9AM-5PM)

    // SNMP настройки
    public string SnmpVersion { get; set; } = "SNMPv2c"; // SNMPv1, SNMPv2c, SNMPv3
    public string SnmpCommunity { get; set; } = "public";
    public int SnmpTimeoutMs { get; set; } = 5000;
    public int SnmpRetries { get; set; } = 3;

    // Email настройки
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string EmailFrom { get; set; } = "";
    public List<string> EmailRecipients { get; set; } = new();

    // Устройства
    public List<DeviceSettings> Devices { get; set; } = new();
}

/// <summary>
/// Настройки конкретного устройства
/// </summary>
public class DeviceSettings
{
    public string HostnameOrIp { get; set; } = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 60;
    public string SnmpCommunity { get; set; } = "public";
    public string SnmpVersion { get; set; } = "SNMPv2c"; // SNMPv1, SNMPv2c, SNMPv3
    public double CriticalThreshold { get; set; } = 10.0;
    public double WarningThreshold { get; set; } = 25.0;
    public DateTime AddedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Аргументы события изменения настроек
/// </summary>
public class SettingsChangedEventArgs : EventArgs
{
    public DesktopSettings OldSettings { get; }
    public DesktopSettings NewSettings { get; }

    public SettingsChangedEventArgs(DesktopSettings oldSettings, DesktopSettings newSettings)
    {
        OldSettings = oldSettings;
        NewSettings = newSettings;
    }
}