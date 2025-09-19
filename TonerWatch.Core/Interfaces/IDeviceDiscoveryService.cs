using System.Net;
using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

public interface IDeviceDiscoveryService
{
    Task<IEnumerable<DiscoveredDevice>> DiscoverDevicesAsync(DiscoverySettings settings, CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscoveredDevice>> ScanSubnetAsync(string subnet, TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscoveredDevice>> DiscoverFromActiveDirectoryAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscoveredDevice>> DiscoverFromWmiAsync(CancellationToken cancellationToken = default);
    Task<bool> TestDeviceConnectivityAsync(string ipAddress, int port = 161, TimeSpan? timeout = null);
}

public class DiscoverySettings
{
    public string SubnetRange { get; set; } = "192.168.1.0/24";
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public bool UseActiveDirectory { get; set; } = true;
    public bool UseWmi { get; set; } = true;
    public bool UseSubnetScan { get; set; } = true;
    public List<int> PortsToScan { get; set; } = new() { 161, 80, 443, 9100, 515 };
    public int MaxConcurrentScans { get; set; } = 50;
    public int ScanRetries { get; set; } = 1;
    public int ScanRetryDelayMs { get; set; } = 1000; // Delay between retries in milliseconds
    public bool EnableSNMPFingerprinting { get; set; } = true; // Enable SNMP-based device fingerprinting
    public bool EnableIncrementalScanning { get; set; } = false; // Enable incremental scanning for already discovered devices
}

public class DiscoveredDevice
{
    public string IpAddress { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DiscoveryMethod DiscoveryMethod { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public List<int> OpenPorts { get; set; } = new();
    public bool SupportsSnmp { get; set; }
    public string? SnmpCommunity { get; set; }
    public DeviceType EstimatedDeviceType { get; set; }
    public double ConfidenceScore { get; set; }
}

public enum DiscoveryMethod
{
    SubnetScan,
    ActiveDirectory,
    WindowsManagementInstrumentation,
    ManualEntry,
    SnmpBroadcast
}

public enum DeviceType
{
    Unknown,
    Printer,
    MultiFunctionDevice,
    Scanner,
    Fax,
    NetworkDevice,
    Computer
}