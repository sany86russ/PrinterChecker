using System.Net;

namespace TonerWatch.Core.Models;

/// <summary>
/// Device entity representing a printer or multifunction device
/// </summary>
public class Device
{
    public int Id { get; set; }
    public required string Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public int SiteId { get; set; }
    public string? Location { get; set; }
    public DeviceCapabilities Capabilities { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastPolled { get; set; }
    public string? SystemObjectId { get; set; } // SNMP sysObjectID for vendor identification
    public string? SystemDescription { get; set; } // SNMP sysDescr
    public int? PageCount { get; set; }
    public int? ColorPageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Tags { get; set; } // JSON array of tags
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Site Site { get; set; } = null!;
    public virtual ICollection<Supply> Supplies { get; set; } = new List<Supply>();
    public virtual ICollection<Counter> Counters { get; set; } = new List<Counter>();
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();
    public virtual ICollection<ForecastSnapshot> ForecastSnapshots { get; set; } = new List<ForecastSnapshot>();
    public virtual ICollection<Credential> Credentials { get; set; } = new List<Credential>();

    /// <summary>
    /// Get IP address as IPAddress object
    /// </summary>
    public IPAddress? GetIPAddress()
    {
        return string.IsNullOrEmpty(IpAddress) ? null : IPAddress.TryParse(IpAddress, out var ip) ? ip : null;
    }

    /// <summary>
    /// Check if device supports a specific capability
    /// </summary>
    public bool HasCapability(DeviceCapabilities capability)
    {
        return Capabilities.HasFlag(capability);
    }
}