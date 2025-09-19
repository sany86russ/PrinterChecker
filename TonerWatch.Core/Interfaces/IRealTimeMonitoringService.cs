using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Service interface for real-time supply level monitoring
/// </summary>
public interface IRealTimeMonitoringService
{
    /// <summary>
    /// Set monitoring configuration
    /// </summary>
    void SetConfiguration(TimeSpan defaultInterval, TimeSpan criticalInterval, double criticalThreshold);
    
    /// <summary>
    /// Check if a device needs monitoring based on its supply levels
    /// </summary>
    Task<bool> ShouldMonitorDeviceAsync(Device device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Monitor a device in real-time
    /// </summary>
    Task<SupplyMonitoringResult> MonitorDeviceAsync(Device device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Monitor multiple devices in parallel
    /// </summary>
    Task<IEnumerable<SupplyMonitoringResult>> MonitorDevicesAsync(
        IEnumerable<Device> devices, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process monitoring results and generate alerts
    /// </summary>
    Task<IEnumerable<Alert>> ProcessMonitoringResultsAsync(
        IEnumerable<SupplyMonitoringResult> results, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of supply monitoring operation
/// </summary>
public class SupplyMonitoringResult
{
    public int DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public List<SupplyReading> Readings { get; set; } = new();
    public string? Error { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(Error);
}