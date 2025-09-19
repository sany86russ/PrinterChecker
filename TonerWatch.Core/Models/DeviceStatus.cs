namespace TonerWatch.Core.Models;

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatus
{
    Unknown = 0,
    Online = 1,
    Offline = 2,
    Warning = 3,
    Error = 4,
    Maintenance = 5,
    StandBy = 6,
    Processing = 7,
    Stopped = 8
}

/// <summary>
/// Device capabilities flags
/// </summary>
[Flags]
public enum DeviceCapabilities
{
    None = 0,
    SNMPv1 = 1,
    SNMPv2c = 2,
    SNMPv3 = 4,
    IPP = 8,
    HTTP = 16,
    PJL = 32,
    ColorPrinting = 64,
    Duplex = 128,
    Fax = 256,
    Scanner = 512,
    Email = 1024,
    WirelessNetwork = 2048,
    TouchScreen = 4096,
    QueueManagement = 8192
}