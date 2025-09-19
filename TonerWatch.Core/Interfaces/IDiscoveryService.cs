using System.Net;
using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Discovery result for a discovered device
/// </summary>
public class DiscoveryResult
{
    public required string Hostname { get; set; }
    public required IPAddress IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public DeviceCapabilities Capabilities { get; set; }
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SystemObjectId { get; set; }
    public string? SystemDescription { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Discovery options for configuring discovery behavior
/// </summary>
public class DiscoveryOptions
{
    public List<string> SubnetRanges { get; set; } = new();
    public bool DiscoverFromActiveDirectory { get; set; } = true;
    public bool DiscoverFromPrintServers { get; set; } = true;
    public bool DiscoverFromSubnetScan { get; set; } = true;
    public bool DiscoverFromMulticast { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrency { get; set; } = 50;
    public List<int> PortsToScan { get; set; } = new() { 161, 631, 9100, 80, 443 };
    public bool EnableSNMPFingerprinting { get; set; } = true;
    public bool EnableHTTPFingerprinting { get; set; } = true;
}

/// <summary>
/// Service interface for device discovery
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Discover devices in the network
    /// </summary>
    Task<IEnumerable<DiscoveryResult>> DiscoverDevicesAsync(DiscoveryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover devices from Active Directory
    /// </summary>
    Task<IEnumerable<DiscoveryResult>> DiscoverFromActiveDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover devices from print servers
    /// </summary>
    Task<IEnumerable<DiscoveryResult>> DiscoverFromPrintServersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover devices by scanning subnet ranges
    /// </summary>
    Task<IEnumerable<DiscoveryResult>> DiscoverFromSubnetScanAsync(IEnumerable<string> subnets, DiscoveryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Probe a specific device for capabilities
    /// </summary>
    Task<DiscoveryResult?> ProbeDeviceAsync(IPAddress ipAddress, DiscoveryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fingerprint device to determine vendor and model
    /// </summary>
    Task<(string? vendor, string? model, string? systemObjectId)> FingerprintDeviceAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
}