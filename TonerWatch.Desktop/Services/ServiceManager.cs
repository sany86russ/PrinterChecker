using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Infrastructure.Services;
using TonerWatch.Discovery;
using TonerWatch.Core.Models;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using TonerWatch.Infrastructure.Data;

namespace TonerWatch.Desktop.Services;

/// <summary>
/// Service manager for dependency injection in WPF Desktop application
/// </summary>
public class ServiceManager
{
    private static ServiceManager? _instance;
    private static readonly object _lock = new();
    private readonly ServiceProvider _serviceProvider;

    private ServiceManager()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    public static ServiceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ServiceManager();
                }
            }
            return _instance;
        }
    }

    public T GetService<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public ILogger<T> GetLogger<T>()
    {
        return _serviceProvider.GetRequiredService<ILogger<T>>();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add HTTP client
        services.AddHttpClient();

        // Register core services
        services.AddSingleton<ISupplyNormalizationService, SupplyNormalizationService>();
        services.AddSingleton<IForecastService, ForecastService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<INotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<NotificationService>>();
            var emailSender = provider.GetRequiredService<IEmailNotificationSender>();
            var telegramSender = provider.GetRequiredService<ITelegramNotificationSender>();
            var webhookSender = provider.GetRequiredService<IWebhookNotificationSender>();
            var notificationHistoryService = provider.GetRequiredService<INotificationHistoryService>();
            return new NotificationService(logger, emailSender, telegramSender, webhookSender, notificationHistoryService);
        });
        services.AddSingleton<INotificationHistoryService, NotificationHistoryService>();
        services.AddSingleton<IReportTemplateService, ReportTemplateService>();
        services.AddSingleton<IRealTimeMonitoringService, RealTimeMonitoringService>();
        services.AddSingleton<IEmailNotificationSender, EmailNotificationSender>();
        services.AddSingleton<ITelegramNotificationSender, TelegramNotificationSender>();
        services.AddSingleton<IWebhookNotificationSender, WebhookNotificationSender>();

        // Register protocol services
        services.AddSingleton<ISnmpProtocol, TonerWatch.Protocols.SNMP.SnmpProtocolService>();
        services.AddSingleton<IIppProtocol, TonerWatch.Protocols.IPP.IppProtocolService>();
        services.AddSingleton<IHttpProtocol, TonerWatch.Protocols.HTTP.HttpProtocolService>();
        services.AddSingleton<IPjlProtocol, TonerWatch.Protocols.PJL.PjlProtocolService>();

        // Register device discovery service with SNMP protocol dependency
        services.AddSingleton<IDeviceDiscoveryService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DeviceDiscoveryService>>();
            var snmpProtocol = provider.GetService<ISnmpProtocol>();
            return new DeviceDiscoveryService(logger, snmpProtocol);
        });

        // Register infrastructure services
        services.AddSingleton<DevicePersistenceService>();
        services.AddSingleton<ExportService>(provider =>
        {
            var context = provider.GetRequiredService<TonerWatchDbContext>();
            var reportTemplateService = provider.GetRequiredService<IReportTemplateService>();
            return new ExportService(context, reportTemplateService);
        });

        // Register desktop services
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<DeviceDataService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DeviceDataService>>();
            var settingsManager = provider.GetRequiredService<SettingsManager>();
            return new DeviceDataService(logger, settingsManager);
        });
        services.AddSingleton<SupplyDataService>();
        
        // Register MonitoringService with SettingsManager dependency
        services.AddSingleton<MonitoringService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<MonitoringService>>();
            var discoveryService = provider.GetRequiredService<IDeviceDiscoveryService>();
            var alertService = provider.GetRequiredService<IAlertService>();
            var notificationService = provider.GetRequiredService<INotificationService>();
            var forecastService = provider.GetRequiredService<IForecastService>();
            var settingsManager = provider.GetRequiredService<SettingsManager>();
            var snmpProtocol = provider.GetRequiredService<ISnmpProtocol>();
            var devicePersistenceService = provider.GetRequiredService<DevicePersistenceService>();
            return new MonitoringService(logger, discoveryService, alertService, notificationService, forecastService, settingsManager, snmpProtocol, devicePersistenceService);
        });

    }

    public void Dispose()
    {
        try
        {
            // Stop all background operations first
            var monitoringService = _serviceProvider.GetService<MonitoringService>();
            monitoringService?.StopMonitoringAsync().Wait(TimeSpan.FromSeconds(2));
            
            // Dispose the monitoring service explicitly
            if (monitoringService is IDisposable disposableMonitoring)
            {
                disposableMonitoring.Dispose();
            }
            
            // Dispose all other services
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            // Log error but don't throw to avoid blocking shutdown
            Console.WriteLine($"Error during ServiceManager disposal: {ex.Message}");
        }
    }
}

/// <summary>
/// Desktop monitoring service to coordinate background tasks
/// </summary>
public class MonitoringService : IDisposable
{
    private readonly ILogger<MonitoringService> _logger;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IAlertService _alertService;
    private readonly INotificationService _notificationService;
    private readonly IForecastService _forecastService;
    private readonly SettingsManager _settingsManager;
    private readonly ISnmpProtocol _snmpProtocol;
    private readonly DevicePersistenceService _devicePersistenceService; // Add persistence service
    private System.Threading.Timer? _monitoringTimer;
    private System.Threading.Timer? _discoveryTimer;
    private bool _isMonitoring;
    private bool _disposed = false;

    public MonitoringService(
        ILogger<MonitoringService> logger,
        IDeviceDiscoveryService discoveryService,
        IAlertService alertService,
        INotificationService notificationService,
        IForecastService forecastService,
        SettingsManager settingsManager,
        ISnmpProtocol snmpProtocol,
        DevicePersistenceService devicePersistenceService) // Add this parameter
    {
        _logger = logger;
        _discoveryService = discoveryService;
        _alertService = alertService;
        _notificationService = notificationService;
        _forecastService = forecastService;
        _settingsManager = settingsManager;
        _snmpProtocol = snmpProtocol;
        _devicePersistenceService = devicePersistenceService; // Store the persistence service

        // Subscribe to settings changes
        _settingsManager.SettingsChanged += OnSettingsChanged;

        // Setup timers for monitoring
        _monitoringTimer = new System.Threading.Timer(MonitorDevices, null, Timeout.Infinite, Timeout.Infinite);
        _discoveryTimer = new System.Threading.Timer(DiscoverDevices, null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler<MonitoringStatusEventArgs>? StatusChanged;
    public event EventHandler<DeviceUpdatedEventArgs>? DeviceUpdated;
    public event EventHandler<AlertGeneratedEventArgs>? AlertGenerated;

    public bool IsMonitoring => _isMonitoring;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!_isMonitoring || _disposed) return;

        try
        {
            _logger.LogInformation("Settings changed, updating monitoring intervals");
            
            // Update monitoring interval
            var monitoringInterval = TimeSpan.FromSeconds(e.NewSettings.RefreshIntervalSeconds);
            _monitoringTimer?.Change(TimeSpan.Zero, monitoringInterval);
            
            // Update discovery interval
            if (e.NewSettings.DiscoveryIntervalMinutes > 0)
            {
                var discoveryInterval = TimeSpan.FromMinutes(e.NewSettings.DiscoveryIntervalMinutes);
                _discoveryTimer?.Change(TimeSpan.FromMinutes(1), discoveryInterval);
            }
            else if (e.NewSettings.DiscoveryIntervalMinutes == -1)
            {
                // Discovery is disabled, stop the timer
                _discoveryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _logger.LogInformation("Device discovery disabled");
            }
            else
            {
                // Default interval if not set
                _discoveryTimer?.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
            }
            
            // Handle subnet range changes dynamically
            if (e.OldSettings.SubnetRange != e.NewSettings.SubnetRange)
            {
                _logger.LogInformation("Subnet range changed from {OldRange} to {NewRange}", 
                    e.OldSettings.SubnetRange, e.NewSettings.SubnetRange);
                // The subnet range will be used in the next discovery cycle
            }
            
            // Handle SNMP enabled changes
            if (e.OldSettings.SnmpEnabled != e.NewSettings.SnmpEnabled)
            {
                _logger.LogInformation("SNMP setting changed to {SnmpEnabled}", e.NewSettings.SnmpEnabled);
            }
            
            // Handle auto discovery changes
            if (e.OldSettings.AutoDiscoveryEnabled != e.NewSettings.AutoDiscoveryEnabled)
            {
                _logger.LogInformation("Auto discovery setting changed to {AutoDiscoveryEnabled}", e.NewSettings.AutoDiscoveryEnabled);
                if (!e.NewSettings.AutoDiscoveryEnabled)
                {
                    // If auto discovery is disabled, stop the discovery timer
                    _discoveryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _logger.LogInformation("Device discovery disabled due to auto discovery setting");
                }
                else
                {
                    // If auto discovery is enabled, restart the discovery timer
                    if (e.NewSettings.DiscoveryIntervalMinutes > 0)
                    {
                        var discoveryInterval = TimeSpan.FromMinutes(e.NewSettings.DiscoveryIntervalMinutes);
                        _discoveryTimer?.Change(TimeSpan.FromMinutes(1), discoveryInterval);
                    }
                    else
                    {
                        // Default interval
                        _discoveryTimer?.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
                    }
                    _logger.LogInformation("Device discovery enabled");
                }
            }
            
            _logger.LogInformation("Monitoring intervals updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating monitoring intervals from settings change");
        }
    }

    public async Task StartMonitoringAsync()
    {
        if (_isMonitoring) return;

        _logger.LogInformation("Starting desktop monitoring service");
        _isMonitoring = true;

        // Get settings from SettingsManager
        var appSettings = _settingsManager.Settings;

        // Start monitoring with interval from settings
        var monitoringInterval = TimeSpan.FromSeconds(appSettings.RefreshIntervalSeconds);
        _monitoringTimer?.Change(TimeSpan.Zero, monitoringInterval);
        
        // Start discovery with interval from settings
        if (appSettings.DiscoveryIntervalMinutes > 0)
        {
            var discoveryInterval = TimeSpan.FromMinutes(appSettings.DiscoveryIntervalMinutes);
            _discoveryTimer?.Change(TimeSpan.FromMinutes(1), discoveryInterval); // Start after 1 minute
        }
        else if (appSettings.DiscoveryIntervalMinutes == -1)
        {
            // Discovery is disabled, stop the timer
            _discoveryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        else
        {
            // Default interval if not set
            _discoveryTimer?.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
        }

        StatusChanged?.Invoke(this, new MonitoringStatusEventArgs(true, "Monitoring started"));
    }

    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring) return;

        _logger.LogInformation("Stopping desktop monitoring service");
        _isMonitoring = false;

        // Stop timers immediately
        _monitoringTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        _discoveryTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

        StatusChanged?.Invoke(this, new MonitoringStatusEventArgs(false, "Monitoring stopped"));
        
        // Wait a moment for any running callbacks to complete
        await Task.Delay(100);
    }

    private async void MonitorDevices(object? state)
    {
        if (!_isMonitoring || _disposed) return;

        try
        {
            _logger.LogDebug("Starting device monitoring cycle");

            // Get settings from SettingsManager (always use latest settings)
            var settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
            var appSettings = settingsManager.Settings;

            // Set SNMP timeout from settings
            if (_snmpProtocol is TonerWatch.Protocols.SNMP.SnmpProtocolService snmpService)
            {
                snmpService.SetTimeout(appSettings.SnmpTimeoutMs);
            }

            // Get devices to monitor
            var devicesToMonitor = appSettings.Devices.Where(d => d.IsActive).ToList();
            
            _logger.LogDebug("Monitoring {DeviceCount} active devices", devicesToMonitor.Count);

            foreach (var deviceSettings in devicesToMonitor)
            {
                if (!_isMonitoring || _disposed) break;

                try
                {
                    _logger.LogDebug("Monitoring device {DeviceName} ({IpAddress})", deviceSettings.Name, deviceSettings.HostnameOrIp);

                    if (!IPAddress.TryParse(deviceSettings.HostnameOrIp, out var ip))
                    {
                        _logger.LogWarning("Invalid IP address for device {DeviceName}: {IpAddress}", deviceSettings.Name, deviceSettings.HostnameOrIp);
                        continue;
                    }

                    // Test connectivity first
                    if (!await _discoveryService.TestDeviceConnectivityAsync(deviceSettings.HostnameOrIp, 161, TimeSpan.FromMilliseconds(appSettings.SnmpTimeoutMs)))
                    {
                        _logger.LogWarning("Device {DeviceName} is not reachable via SNMP", deviceSettings.Name);
                        
                        // Update device status to offline
                        var existingDevice = await _devicePersistenceService.GetDeviceByIpAsync(deviceSettings.HostnameOrIp);
                        if (existingDevice != null)
                        {
                            await _devicePersistenceService.UpdateDeviceStatusAsync(existingDevice.Id, DeviceStatus.Offline);
                        }
                        continue;
                    }

                    // Get device info via SNMP
                    var credential = new Credential
                    {
                        Type = deviceSettings.SnmpVersion switch
                        {
                            "SNMPv1" => CredentialType.SNMPv1,
                            "SNMPv2c" => CredentialType.SNMPv2c,
                            "SNMPv3" => CredentialType.SNMPv3,
                            _ => CredentialType.SNMPv2c
                        },
                        Name = "Default SNMP Credential",
                        SecretRef = deviceSettings.SnmpCommunity
                    };
                
                    var deviceInfo = await _snmpProtocol.GetDeviceInfoAsync(ip, credential);
                    if (deviceInfo == null)
                    {
                        _logger.LogWarning("Failed to get device info for {DeviceName}", deviceSettings.Name);
                        continue;
                    }

                    // Save or update device in database
                    var discoveredDevice = new DiscoveredDevice
                    {
                        IpAddress = deviceSettings.HostnameOrIp,
                        Hostname = deviceSettings.Name,
                        Manufacturer = deviceInfo.Vendor,
                        Model = deviceInfo.Model,
                        EstimatedDeviceType = DeviceType.Printer
                    };
                
                    var dbDevice = await _devicePersistenceService.SaveDeviceAsync(discoveredDevice);
                
                    // Get supply levels via SNMP
                    var supplyReadings = await _snmpProtocol.GetSupplyLevelsAsync(ip, credential);
                    
                    // Save supply readings to database
                    await _devicePersistenceService.SaveSupplyReadingsAsync(dbDevice.Id, supplyReadings);
                    
                    // Generate forecasts
                    // Note: In a real implementation, we would save this data to the database
                    var forecasts = new List<ForecastSnapshot>(); // Placeholder
                    
                    // Check for alerts
                    var alerts = new List<Alert>(); // Placeholder - would use real alert service
                    
                    foreach (var supply in supplyReadings)
                    {
                        if (!_isMonitoring || _disposed) break;
                        
                        // Create alert context for each supply
                        var context = new AlertContext
                        {
                            Device = dbDevice,
                            Supply = new Supply { Kind = supply.Kind, Name = supply.Name },
                            CurrentValue = supply.Percent,
                            PreviousValue = null // Would need to get from database
                        };
                        
                        // Check if we need to generate an alert
                        var alert = await _alertService.EvaluateSupplyAlertAsync(context);
                        if (alert != null)
                        {
                            alerts.Add(alert);
                            
                            AlertGenerated?.Invoke(this, new AlertGeneratedEventArgs(alert));
                            
                            // Send notifications for the alert based on settings
                            if (appSettings.WindowsNotificationsEnabled || appSettings.EmailNotificationsEnabled)
                            {
                                await _notificationService.SendAlertNotificationAsync(alert);
                            }
                        }
                    }

                    if (!_disposed)
                    {
                        DeviceUpdated?.Invoke(this, new DeviceUpdatedEventArgs(dbDevice.Id, forecasts.Count(), alerts.Count()));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring device {DeviceName}", deviceSettings.Name);
                }
            }

            // Process pending notifications
            if (_isMonitoring && !_disposed)
            {
                await _notificationService.ProcessPendingNotificationsAsync();
            }

            _logger.LogDebug("Device monitoring cycle completed");
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                _logger.LogError(ex, "Error during device monitoring");
            }
        }
    }

    private async void DiscoverDevices(object? state)
    {
        if (!_isMonitoring || _disposed) return;

        try
        {
            _logger.LogInformation("Starting device discovery");

            // Get settings from SettingsManager (always use latest settings)
            var settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
            var appSettings = settingsManager.Settings;

            // Enhanced discovery settings with performance tuning from app settings
            var settings = new DiscoverySettings
            {
                UseSubnetScan = appSettings.AutoDiscoveryEnabled,
                UseActiveDirectory = false, // Disable AD for desktop to avoid permissions issues
                UseWmi = true,
                SubnetRange = appSettings.SubnetRange,
                PortsToScan = new List<int> { 161, 9100 },
                ScanTimeout = TimeSpan.FromSeconds(appSettings.ScanTimeoutSeconds),
                MaxConcurrentScans = appSettings.MaxConcurrentScans,
                ScanRetries = appSettings.ScanRetries,
                ScanRetryDelayMs = appSettings.ScanRetryDelayMs,
                EnableSNMPFingerprinting = appSettings.SnmpEnabled // Use SNMP settings to control fingerprinting
            };

            var devices = await _discoveryService.DiscoverDevicesAsync(settings);
    
            _logger.LogInformation("Discovery completed. Found {DeviceCount} devices", devices.Count());

            // Persist discovered devices to database
            var devicePersistenceService = ServiceManager.Instance.GetService<DevicePersistenceService>();
            foreach (var device in devices)
            {
                try
                {
                    await devicePersistenceService.SaveDeviceAsync(device);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error persisting discovered device {IpAddress}", device.IpAddress);
                }
            }

            if (!_disposed)
            {
                StatusChanged?.Invoke(this, new MonitoringStatusEventArgs(true, $"Discovery: {devices.Count()} devices found"));
            }
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                _logger.LogWarning(ex, "Error during device discovery");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            _logger.LogInformation("Disposing MonitoringService");
            
            _disposed = true;
            _isMonitoring = false;
            
            // Stop and dispose timers aggressively
            try
            {
                _monitoringTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _discoveryTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                
                // Wait briefly for any callbacks to complete
                Thread.Sleep(100);
                
                _monitoringTimer?.Dispose();
                _discoveryTimer?.Dispose();
                
                // Nullify references to prevent further use
                _monitoringTimer = null;
                _discoveryTimer = null;
            }
            catch (ObjectDisposedException)
            {
                // Timer already disposed, ignore
            }
            
            // Clear event handlers to prevent memory leaks
            StatusChanged = null;
            DeviceUpdated = null;
            AlertGenerated = null;
            
            _logger.LogInformation("MonitoringService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MonitoringService");
        }
    }
}

/// <summary>
/// Service for managing device data in the desktop application
/// </summary>
public class DeviceDataService
{
    private readonly ILogger<DeviceDataService> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly List<DeviceViewModel> _devices;

    public DeviceDataService(ILogger<DeviceDataService> logger, SettingsManager settingsManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _devices = new List<DeviceViewModel>();
        LoadDevicesFromSettings();
        
        // Subscribe to settings changes to update devices
        _settingsManager.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        // Check if devices have changed
        if (!e.OldSettings.Devices.SequenceEqual(e.NewSettings.Devices, new DeviceSettingsComparer()))
        {
            LoadDevicesFromSettings();
            DataChanged?.Invoke(this, new DeviceDataChangedEventArgs(new DeviceViewModel())); // Trigger data change event
        }
    }
    
    // Add comparer for DeviceSettings
    private class DeviceSettingsComparer : IEqualityComparer<DeviceSettings>
    {
        public bool Equals(DeviceSettings? x, DeviceSettings? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.HostnameOrIp == y.HostnameOrIp && 
                   x.Name == y.Name && 
                   x.Location == y.Location &&
                   x.IsActive == y.IsActive;
        }

        public int GetHashCode(DeviceSettings obj)
        {
            return HashCode.Combine(obj.HostnameOrIp, obj.Name, obj.Location, obj.IsActive);
        }
    }

    public event EventHandler<DeviceDataChangedEventArgs>? DataChanged;

    public IEnumerable<DeviceViewModel> GetDevices() => _devices;

    public DeviceViewModel? GetDevice(int id) => _devices.FirstOrDefault(d => d.Id == id);

    public DeviceInfoViewModel? GetDeviceInfo(string hostnameOrIp)
    {
        var device = _devices.FirstOrDefault(d => d.IpAddress == hostnameOrIp || d.Name.Contains(hostnameOrIp));
        if (device == null) return null;
        
        return new DeviceInfoViewModel
        {
            Name = device.Name,
            HostnameOrIp = device.IpAddress,
            Location = device.Location,
            IsOnline = (DateTime.Now - device.LastSeen).TotalMinutes < 10,
            LastSeen = device.LastSeen,
            Status = device.Status
        };
    }

    public void UpdateDeviceStatus(int deviceId, int forecastCount, int alertCount)
    {
        var device = GetDevice(deviceId);
        if (device != null)
        {
            device.LastSeen = DateTime.Now;
            device.ForecastCount = forecastCount;
            device.AlertCount = alertCount;
            device.Status = alertCount > 0 ? "⚠️ Alerts" : "✅ OK";
            
            DataChanged?.Invoke(this, new DeviceDataChangedEventArgs(device));
        }
    }

    private void LoadDevicesFromSettings()
    {
        try
        {
            var settings = _settingsManager.Settings;
            _devices.Clear();
            
            for (int i = 0; i < settings.Devices.Count; i++)
            {
                var deviceSettings = settings.Devices[i];
                var device = new DeviceViewModel
                {
                    Id = i + 1,
                    Name = deviceSettings.Name,
                    Location = deviceSettings.Location,
                    IpAddress = deviceSettings.HostnameOrIp,
                    Status = "🔄 Checking...", // Initial status
                    LastSeen = DateTime.MinValue,
                    ForecastCount = 0,
                    AlertCount = 0
                };
                _devices.Add(device);
            }
            
            _logger.LogInformation("Loaded {DeviceCount} devices from settings", _devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading devices from settings");
        }
    }

    private List<DeviceViewModel> GenerateSimulatedDevices()
    {
        // Return empty list instead of hardcoded demo devices
        return new List<DeviceViewModel>();
    }
}

/// <summary>
/// Service for managing supply data
/// </summary>
public class SupplyDataService
{
    private readonly ILogger<SupplyDataService> _logger;
    private readonly Random _random = new();

    public SupplyDataService(ILogger<SupplyDataService> logger)
    {
        _logger = logger;
    }

    public SupplyStatus GetSupplyStatus(int deviceId)
    {
        // Simulate different supply levels for demo
        return new SupplyStatus
        {
            BlackToner = GenerateSupplyLevel(deviceId, 1),
            CyanToner = GenerateSupplyLevel(deviceId, 2),
            MagentaToner = GenerateSupplyLevel(deviceId, 3),
            YellowToner = GenerateSupplyLevel(deviceId, 4),
            DrumUnit = GenerateSupplyLevel(deviceId, 5),
            MaintenanceKit = GenerateSupplyLevel(deviceId, 6)
        };
    }
    
    public IEnumerable<SupplyViewModel>? GetSupplyStatus(string hostnameOrIp)
    {
        try
        {
            // In a real implementation, this would fetch actual supply data from the device
            // For now, return null to indicate no data available
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supply status for {HostnameOrIp}", hostnameOrIp);
            return null;
        }
    }

    private int GenerateSupplyLevel(int deviceId, int supplyType)
    {
        // Generate consistent but varying levels based on device and supply type
        var seed = deviceId * 100 + supplyType * 10;
        var random = new Random(seed);
        
        return supplyType switch
        {
            1 => random.Next(15, 95), // Black toner - more usage
            2 => random.Next(25, 90), // Cyan toner
            3 => random.Next(5, 85),  // Magenta toner - sometimes critical
            4 => random.Next(30, 90), // Yellow toner
            5 => random.Next(40, 95), // Drum unit - lasts longer
            6 => random.Next(60, 99), // Maintenance kit - rarely needs replacement
            _ => random.Next(20, 80)
        };
    }
    
    private List<SupplyViewModel> GenerateSimulatedSupplies()
    {
        // Return empty list instead of demo data
        return new List<SupplyViewModel>();
    }
}

// Event argument classes
public class MonitoringStatusEventArgs : EventArgs
{
    public bool IsRunning { get; }
    public string Message { get; }

    public MonitoringStatusEventArgs(bool isRunning, string message)
    {
        IsRunning = isRunning;
        Message = message;
    }
}

public class DeviceUpdatedEventArgs : EventArgs
{
    public int DeviceId { get; }
    public int ForecastCount { get; }
    public int AlertCount { get; }

    public DeviceUpdatedEventArgs(int deviceId, int forecastCount, int alertCount)
    {
        DeviceId = deviceId;
        ForecastCount = forecastCount;
        AlertCount = alertCount;
    }
}

public class AlertGeneratedEventArgs : EventArgs
{
    public Alert Alert { get; }

    public AlertGeneratedEventArgs(Alert alert)
    {
        Alert = alert;
    }
}

public class DeviceDataChangedEventArgs : EventArgs
{
    public DeviceViewModel Device { get; }

    public DeviceDataChangedEventArgs(DeviceViewModel device)
    {
        Device = device;
    }
}

// View models
public class DeviceViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public int ForecastCount { get; set; }
    public int AlertCount { get; set; }
}

public class SupplyStatus
{
    public int BlackToner { get; set; }
    public int CyanToner { get; set; }
    public int MagentaToner { get; set; }
    public int YellowToner { get; set; }
    public int DrumUnit { get; set; }
    public int MaintenanceKit { get; set; }
}

public class SupplyViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public double Percent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Unit { get; set; } = "pages";
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class DeviceInfoViewModel
{
    public string Name { get; set; } = string.Empty;
    public string HostnameOrIp { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<SupplyViewModel> Supplies { get; set; } = new();
}