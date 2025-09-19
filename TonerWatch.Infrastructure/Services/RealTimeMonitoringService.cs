using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using System.Collections.Concurrent;

namespace TonerWatch.Infrastructure.Services;

/// <summary>
/// Service for real-time supply level monitoring via SNMP
/// </summary>
public class RealTimeMonitoringService : IRealTimeMonitoringService
{
    private readonly ILogger<RealTimeMonitoringService> _logger;
    private readonly ISnmpProtocol _snmpProtocol;
    private readonly IAlertService _alertService;
    private readonly IForecastService _forecastService;
    private readonly ConcurrentDictionary<int, DateTime> _lastCheckTimes = new();
    private readonly ConcurrentDictionary<int, SupplyReading> _lastReadings = new();
    
    // Monitoring intervals (configurable)
    private TimeSpan _defaultCheckInterval = TimeSpan.FromMinutes(5);
    private TimeSpan _criticalCheckInterval = TimeSpan.FromMinutes(1);
    private double _criticalThreshold = 15.0; // Percentage

    public RealTimeMonitoringService(
        ILogger<RealTimeMonitoringService> logger,
        ISnmpProtocol snmpProtocol,
        IAlertService alertService,
        IForecastService forecastService)
    {
        _logger = logger;
        _snmpProtocol = snmpProtocol;
        _alertService = alertService;
        _forecastService = forecastService;
    }

    /// <summary>
    /// Set monitoring configuration
    /// </summary>
    public void SetConfiguration(TimeSpan defaultInterval, TimeSpan criticalInterval, double criticalThreshold)
    {
        _defaultCheckInterval = defaultInterval;
        _criticalCheckInterval = criticalInterval;
        _criticalThreshold = criticalThreshold;
    }

    /// <summary>
    /// Check if a device needs monitoring based on its supply levels
    /// </summary>
    public async Task<bool> ShouldMonitorDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        // Check if enough time has passed since last check
        if (_lastCheckTimes.TryGetValue(device.Id, out var lastCheck))
        {
            var timeSinceLastCheck = DateTime.UtcNow - lastCheck;
            
            // If we have previous readings, check if any supply is critical
            if (_lastReadings.TryGetValue(device.Id, out var lastReading))
            {
                // For critical supplies, check more frequently
                if (lastReading.Percent <= _criticalThreshold)
                {
                    return timeSinceLastCheck >= _criticalCheckInterval;
                }
            }
            
            // Otherwise, use default interval
            return timeSinceLastCheck >= _defaultCheckInterval;
        }
        
        // If no previous check, monitor now
        return true;
    }

    /// <summary>
    /// Monitor a device in real-time
    /// </summary>
    public async Task<SupplyMonitoringResult> MonitorDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Monitoring device {DeviceId} - {DeviceName}", device.Id, device.Hostname);

        var result = new SupplyMonitoringResult
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Get current supply levels via SNMP
            var supplyReadings = await _snmpProtocol.GetSupplyLevelsAsync(
                string.IsNullOrEmpty(device.IpAddress) ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Parse(device.IpAddress), 
                null, // No credentials for now
                cancellationToken);

            result.Readings = supplyReadings.ToList();
            
            // Update last check time
            _lastCheckTimes[device.Id] = DateTime.UtcNow;
            
            // Store last readings for critical supply detection
            if (supplyReadings.Any())
            {
                // Use the lowest percentage reading as the representative
                var lowestReading = supplyReadings.OrderBy(r => r.Percent).First();
                _lastReadings[device.Id] = lowestReading;
            }

            _logger.LogInformation("Successfully monitored device {DeviceId}, retrieved {SupplyCount} supply readings", 
                device.Id, supplyReadings.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to monitor device {DeviceId}", device.Id);
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Monitor multiple devices in parallel
    /// </summary>
    public async Task<IEnumerable<SupplyMonitoringResult>> MonitorDevicesAsync(
        IEnumerable<Device> devices, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Monitoring {DeviceCount} devices", devices.Count());

        var tasks = devices.Select(device => MonitorDeviceAsync(device, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results;
    }

    /// <summary>
    /// Process monitoring results and generate alerts
    /// </summary>
    public async Task<IEnumerable<Alert>> ProcessMonitoringResultsAsync(
        IEnumerable<SupplyMonitoringResult> results, 
        CancellationToken cancellationToken = default)
    {
        var alerts = new List<Alert>();

        foreach (var result in results.Where(r => !string.IsNullOrEmpty(r.Error)))
        {
            // Create device offline alert if monitoring failed
            var context = new AlertContext
            {
                Device = new Device { Id = result.DeviceId, Hostname = "Unknown" },
                Timestamp = result.Timestamp
            };

            // In a real implementation, we would get the actual device from database
            // For now, we'll skip creating alerts for failed monitoring
        }

        return alerts;
    }
}

