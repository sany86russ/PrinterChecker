using System.Text.Json.Serialization;

namespace TonerWatch.Core.Models;

/// <summary>
/// Represents a network segment with its specific configuration
/// </summary>
public class NetworkSegment
{
    /// <summary>
    /// Unique identifier for the network segment
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the network segment
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the network segment
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// IP range for this segment (supports CIDR, range format, comma-separated multiple ranges)
    /// </summary>
    public string IpRange { get; set; } = string.Empty;

    /// <summary>
    /// Whether this segment is enabled for scanning
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Scan timeout for this segment
    /// </summary>
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum concurrent scans for this segment
    /// </summary>
    public int MaxConcurrentScans { get; set; } = 50;

    /// <summary>
    /// Number of retries for failed scans
    /// </summary>
    public int ScanRetries { get; set; } = 1;

    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int ScanRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to enable SNMP fingerprinting for this segment
    /// </summary>
    public bool EnableSNMPFingerprinting { get; set; } = true;

    /// <summary>
    /// Ports to scan for this segment
    /// </summary>
    public List<int> PortsToScan { get; set; } = new() { 161, 80, 443, 9100, 515 };

    /// <summary>
    /// Last scan time for this segment
    /// </summary>
    public DateTime LastScanTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Next scheduled scan time for this segment
    /// </summary>
    public DateTime NextScanTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Whether this segment has specific scheduling
    /// </summary>
    public bool HasCustomSchedule { get; set; } = false;

    /// <summary>
    /// Custom scan schedule (cron format or similar)
    /// </summary>
    public string? CustomSchedule { get; set; }

    /// <summary>
    /// Whether to avoid peak usage times for this segment
    /// </summary>
    public bool AvoidPeakTimes { get; set; } = false;
}