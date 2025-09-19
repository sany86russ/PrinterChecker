using System.Net;
using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Supply reading result from protocol
/// </summary>
public class SupplyReading
{
    public SupplyKind Kind { get; set; }
    public string? Name { get; set; }
    public string? PartNumber { get; set; }
    public double? Percent { get; set; }
    public int? LevelRaw { get; set; }
    public int? MaxRaw { get; set; }
    public string? Unit { get; set; }
    public Dictionary<string, object> RawData { get; set; } = new();
}

/// <summary>
/// Device information result from protocol
/// </summary>
public class DeviceInfo
{
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? SystemObjectId { get; set; }
    public string? SystemDescription { get; set; }
    public int? PageCount { get; set; }
    public int? ColorPageCount { get; set; }
    public DeviceStatus Status { get; set; }
    public DeviceCapabilities Capabilities { get; set; }
    public Dictionary<string, object> RawData { get; set; } = new();
}

/// <summary>
/// Base interface for printer communication protocols
/// </summary>
public interface IPrinterProtocol
{
    string Name { get; }
    Task<bool> IsAvailableAsync(IPAddress ipAddress, CancellationToken cancellationToken = default);
    Task<DeviceInfo?> GetDeviceInfoAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<SupplyReading>> GetSupplyLevelsAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// SNMP-specific protocol interface
/// </summary>
public interface ISnmpProtocol : IPrinterProtocol
{
    Task<string?> GetSysObjectIdAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<string?> GetSysDescrAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> WalkOidAsync(IPAddress ipAddress, string oidRoot, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<object?> GetOidValueAsync(IPAddress ipAddress, string oid, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<bool> DiscoverPrinterAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// IPP-specific protocol interface
/// </summary>
public interface IIppProtocol : IPrinterProtocol
{
    Task<Dictionary<string, object>> GetPrinterAttributesAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetJobAttributesAsync(IPAddress ipAddress, int jobId, Credential? credential = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP-specific protocol interface for vendor status pages
/// </summary>
public interface IHttpProtocol : IPrinterProtocol
{
    Task<string?> GetStatusPageAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> ParseVendorStatusAsync(IPAddress ipAddress, string vendor, Credential? credential = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// PJL-specific protocol interface
/// </summary>
public interface IPjlProtocol : IPrinterProtocol
{
    Task<Dictionary<string, string>> SendCommandAsync(IPAddress ipAddress, string command, CancellationToken cancellationToken = default);
    Task<string?> GetInfoAsync(IPAddress ipAddress, string category, CancellationToken cancellationToken = default);
}