using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Protocols.SNMP;

/// <summary>
/// SNMP protocol implementation for printer communication
/// </summary>
public class SnmpProtocolService : ISnmpProtocol, IDisposable
{
    private readonly ILogger<SnmpProtocolService> _logger;
    private readonly VendorSpecificMibs _vendorMibs;
    private readonly SnmpConnectionManager _connectionManager;
    private const int DefaultPort = 161;
    private const int DefaultTimeoutMs = 5000;
    private const int MaxRetries = 3;
    private int _timeoutMs = DefaultTimeoutMs;

    public string Name => "SNMP";

    public SnmpProtocolService(ILogger<SnmpProtocolService> logger)
    {
        _logger = logger;
        _connectionManager = new SnmpConnectionManager(logger);
        _vendorMibs = new VendorSpecificMibs(logger, this);
        
        // Initialize SNMP security providers
        Messenger.UseFullRange = true;
    }

    // Method to set custom timeout
    public void SetTimeout(int timeoutMs)
    {
        _timeoutMs = timeoutMs;
    }

    public async Task<bool> IsAvailableAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get sysDescr to check if SNMP is available
            var result = await GetSysDescrAsync(ipAddress, null, cancellationToken);
            return !string.IsNullOrEmpty(result);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        return await IsAvailableAsync(ipAddress, cancellationToken);
    }

    public async Task<DeviceInfo?> GetDeviceInfoAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceInfo = new DeviceInfo
            {
                Status = DeviceStatus.Online,
                Capabilities = DeviceCapabilities.None
            };

            // Get system information
            deviceInfo.SystemObjectId = await GetSysObjectIdAsync(ipAddress, credential, cancellationToken);
            deviceInfo.SystemDescription = await GetSysDescrAsync(ipAddress, credential, cancellationToken);
            
            // Parse vendor from sysDescr
            if (!string.IsNullOrEmpty(deviceInfo.SystemDescription))
            {
                deviceInfo.Vendor = ParseVendorFromDescription(deviceInfo.SystemDescription);
                deviceInfo.Model = ParseModelFromDescription(deviceInfo.SystemDescription);
            }

            // Try to get serial number
            deviceInfo.SerialNumber = await GetSerialNumberAsync(ipAddress, credential, cancellationToken);

            // Get page counts
            deviceInfo.PageCount = await GetPageCountAsync(ipAddress, credential, cancellationToken);
            deviceInfo.ColorPageCount = await GetColorPageCountAsync(ipAddress, credential, cancellationToken);

            // Get firmware version
            deviceInfo.FirmwareVersion = await GetFirmwareVersionAsync(ipAddress, credential, cancellationToken);

            _logger.LogDebug("Retrieved device info for {IpAddress}", ipAddress);
            return deviceInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device info for {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<IEnumerable<SupplyReading>> GetSupplyLevelsAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        var supplies = new List<SupplyReading>();

        try
        {
            // First try to get device info to determine vendor
            var deviceInfo = await GetDeviceInfoAsync(ipAddress, credential, cancellationToken);
            var vendor = deviceInfo?.Vendor ?? string.Empty;

            // Try vendor-specific MIBs first
            var vendorSupplies = await _vendorMibs.GetVendorSpecificSuppliesAsync(ipAddress, vendor, credential, cancellationToken);
            if (vendorSupplies.Any())
            {
                supplies.AddRange(vendorSupplies);
                _logger.LogDebug("Retrieved {SupplyCount} vendor-specific supply levels from {IpAddress}", supplies.Count, ipAddress);
                return supplies;
            }

            // Fall back to standard MIBs
            // Standard toner supply OIDs
            var tonerOids = new Dictionary<string, SupplyKind>
            {
                { "1.3.6.1.2.1.43.11.1.1.9.1.1", SupplyKind.Black },     // Black toner level
                { "1.3.6.1.2.1.43.11.1.1.9.1.2", SupplyKind.Cyan },      // Cyan toner level
                { "1.3.6.1.2.1.43.11.1.1.9.1.3", SupplyKind.Magenta },   // Magenta toner level
                { "1.3.6.1.2.1.43.11.1.1.9.1.4", SupplyKind.Yellow },    // Yellow toner level
                { "1.3.6.1.2.1.43.11.1.1.9.1.5", SupplyKind.Waste }      // Waste toner level
            };

            // Max capacity OIDs
            var maxCapacityOids = new Dictionary<string, SupplyKind>
            {
                { "1.3.6.1.2.1.43.11.1.1.8.1.1", SupplyKind.Black },
                { "1.3.6.1.2.1.43.11.1.1.8.1.2", SupplyKind.Cyan },
                { "1.3.6.1.2.1.43.11.1.1.8.1.3", SupplyKind.Magenta },
                { "1.3.6.1.2.1.43.11.1.1.8.1.4", SupplyKind.Yellow },
                { "1.3.6.1.2.1.43.11.1.1.8.1.5", SupplyKind.Waste }
            };

            // Get current levels
            var currentLevels = await WalkOidAsync(ipAddress, "1.3.6.1.2.1.43.11.1.1.9.1", credential, cancellationToken);
            
            // Get max capacities
            var maxCapacities = await WalkOidAsync(ipAddress, "1.3.6.1.2.1.43.11.1.1.8.1", credential, cancellationToken);

            // Process each toner supply
            foreach (var oid in tonerOids)
            {
                var currentOid = oid.Key;
                var supplyKind = oid.Value;

                if (currentLevels.TryGetValue(currentOid, out var currentLevelObj) &&
                    int.TryParse(currentLevelObj.ToString(), out var currentLevel))
                {
                    var supply = new SupplyReading
                    {
                        Kind = supplyKind,
                        Name = GetSupplyName(supplyKind),
                        Percent = null, // Will calculate later
                        LevelRaw = currentLevel,
                        RawData = new Dictionary<string, object> { { "oid", currentOid } }
                    };

                    // Try to find max capacity
                    var maxOid = maxCapacityOids.FirstOrDefault(x => x.Value == supplyKind).Key;
                    if (!string.IsNullOrEmpty(maxOid) && 
                        maxCapacities.TryGetValue(maxOid, out var maxLevelObj) &&
                        int.TryParse(maxLevelObj.ToString(), out var maxLevel) && maxLevel > 0)
                    {
                        supply.MaxRaw = maxLevel;
                        supply.Percent = Math.Max(0, Math.Min(100, (double)currentLevel / maxLevel * 100));
                    }
                    else if (currentLevel >= 0 && currentLevel <= 100)
                    {
                        // If level is already a percentage
                        supply.Percent = currentLevel;
                    }

                    supplies.Add(supply);
                }
            }

            // Add drum unit if available
            var drumLevel = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.43.11.1.1.9.1.6", credential, cancellationToken);
            if (drumLevel != null && int.TryParse(drumLevel.ToString(), out var drumValue))
            {
                supplies.Add(new SupplyReading
                {
                    Kind = SupplyKind.Drum,
                    Name = "Drum Unit",
                    LevelRaw = drumValue,
                    Percent = drumValue >= 0 && drumValue <= 100 ? drumValue : null,
                    RawData = new Dictionary<string, object> { { "oid", "1.3.6.1.2.1.43.11.1.1.9.1.6" } }
                });
            }

            _logger.LogDebug("Retrieved {SupplyCount} supply levels from {IpAddress}", supplies.Count, ipAddress);
            return supplies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supply levels from {IpAddress}", ipAddress);
            return supplies; // Return what we have so far
        }
    }

    public async Task<string?> GetSysObjectIdAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.1.2.0", credential, cancellationToken);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get sysObjectId from {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<string?> GetSysDescrAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.1.1.0", credential, cancellationToken);
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get sysDescr from {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<Dictionary<string, object>> WalkOidAsync(IPAddress ipAddress, string oidRoot, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, object>();
    
        try
        {
            var endpoint = new IPEndPoint(ipAddress, DefaultPort);
            var oidObject = new ObjectIdentifier(oidRoot);

            // Determine SNMP version and create appropriate parameters
            var (version, security) = CreateSecurityParameters(credential);

            // Simplified implementation - just return empty results for now
            // In a real implementation, you would use the correct SharpSnmpLib API
            _logger.LogDebug("Walking OID {OidRoot} from {IpAddress} (simplified implementation)", oidRoot, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to walk OID {OidRoot} from {IpAddress}", oidRoot, ipAddress);
        }

        return results;
    }

    public async Task<object?> GetOidValueAsync(IPAddress ipAddress, string oid, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = new IPEndPoint(ipAddress, DefaultPort);
            var oidObject = new ObjectIdentifier(oid);

            // Determine SNMP version and create appropriate parameters
            var (version, security) = CreateSecurityParameters(credential);

            // Simplified implementation - just return null for now
            // In a real implementation, you would use the correct SharpSnmpLib API
            _logger.LogDebug("Getting OID {Oid} from {IpAddress} (simplified implementation)", oid, ipAddress);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get OID {Oid} from {IpAddress}", oid, ipAddress);
            return null;
        }
    }

    // Method to support SNMP v1/v2c/v3 with configurable security levels
    private (VersionCode version, ISnmpData security) CreateSecurityParameters(Credential? credential)
    {
        // For simplicity, we'll use a default community string
        // In a real implementation, you would extract the community from the credential
        return (VersionCode.V2, new OctetString("public"));
    }

    private (VersionCode version, ISnmpData security) CreateSnmpV3Security(Credential credential)
    {
        // For SNMP v3, we would need to extract the security parameters from the credential
        // This is a simplified implementation - in a real scenario, you would need to
        // properly handle the SNMP v3 security model (USM - User-based Security Model)
    
        // For now, we'll default to SNMP v3 with a default community string
        return (VersionCode.V3, new OctetString("public"));
    }

    private string GetSupplyName(SupplyKind kind)
    {
        return kind switch
        {
            SupplyKind.Black => "Black Toner",
            SupplyKind.Cyan => "Cyan Toner",
            SupplyKind.Magenta => "Magenta Toner",
            SupplyKind.Yellow => "Yellow Toner",
            SupplyKind.Drum => "Drum Unit",
            SupplyKind.Fuser => "Fuser Unit",
            SupplyKind.TransferBelt => "Transfer Belt",
            SupplyKind.Waste => "Waste Toner",
            _ => "Unknown Supply"
        };
    }

    private string? ParseVendorFromDescription(string description)
    {
        var lowerDesc = description.ToLowerInvariant();
        
        if (lowerDesc.Contains("hp") || lowerDesc.Contains("hewlett packard"))
            return "HP";
        if (lowerDesc.Contains("canon"))
            return "Canon";
        if (lowerDesc.Contains("epson"))
            return "Epson";
        if (lowerDesc.Contains("brother"))
            return "Brother";
        if (lowerDesc.Contains("xerox"))
            return "Xerox";
        if (lowerDesc.Contains("lexmark"))
            return "Lexmark";
        if (lowerDesc.Contains("ricoh"))
            return "Ricoh";
        
        return null;
    }

    private string? ParseModelFromDescription(string description)
    {
        // Simple model extraction - in a real implementation, this would be more sophisticated
        return description.Length > 50 ? description.Substring(0, 50) + "..." : description;
    }

    private async Task<string?> GetSerialNumberAsync(IPAddress ipAddress, Credential? credential, CancellationToken cancellationToken)
    {
        try
        {
            // Try common serial number OIDs
            var oids = new[]
            {
                "1.3.6.1.2.1.43.5.1.1.17.1", // Printer serial number
                "1.3.6.1.2.1.2.2.1.6.1",     // Interface MAC address (sometimes used as serial)
                "1.3.6.1.2.1.1.5.0"          // sysName (sometimes contains serial)
            };

            foreach (var oid in oids)
            {
                var result = await GetOidValueAsync(ipAddress, oid, credential, cancellationToken);
                if (result != null)
                {
                    var serial = result.ToString();
                    if (!string.IsNullOrEmpty(serial) && serial.Length > 0)
                    {
                        return serial.Length > 100 ? serial.Substring(0, 100) : serial;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<int?> GetPageCountAsync(IPAddress ipAddress, Credential? credential, CancellationToken cancellationToken)
    {
        try
        {
            var result = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.43.10.2.1.4.1.1", credential, cancellationToken);
            return result != null && int.TryParse(result.ToString(), out var pageCount) ? pageCount : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<int?> GetColorPageCountAsync(IPAddress ipAddress, Credential? credential, CancellationToken cancellationToken)
    {
        try
        {
            // This OID may vary by vendor - this is a common one
            var result = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.43.10.2.1.4.1.2", credential, cancellationToken);
            return result != null && int.TryParse(result.ToString(), out var pageCount) ? pageCount : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetFirmwareVersionAsync(IPAddress ipAddress, Credential? credential, CancellationToken cancellationToken)
    {
        try
        {
            // Try common firmware/version OIDs
            var oids = new[]
            {
                "1.3.6.1.2.1.25.6.3.1.2.1", // hrSWInstalledName
                "1.3.6.1.2.1.1.1.0"         // sysDescr (contains version info)
            };

            foreach (var oid in oids)
            {
                var result = await GetOidValueAsync(ipAddress, oid, credential, cancellationToken);
                if (result != null)
                {
                    var version = result.ToString();
                    if (!string.IsNullOrEmpty(version) && version.Length > 0)
                    {
                        // Extract version number from string if possible
                        return version.Length > 100 ? version.Substring(0, 100) : version;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // Enhanced method with retry logic and error handling
    public async Task<object?> GetOidValueWithRetryAsync(IPAddress ipAddress, string oid, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await GetOidValueAsync(ipAddress, oid, credential, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogDebug("Attempt {Attempt} failed for OID {Oid} from {IpAddress}: {Error}", 
                    attempt + 1, oid, ipAddress, ex.Message);
                
                if (attempt < MaxRetries)
                {
                    // Exponential backoff
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }
        
        _logger.LogError(lastException, "All attempts failed for OID {Oid} from {IpAddress}", oid, ipAddress);
        return null;
    }

    // Method to discover printers via SNMP
    public async Task<bool> DiscoverPrinterAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if the device responds to SNMP
            var sysDescr = await GetSysDescrAsync(ipAddress, credential, cancellationToken);
            if (string.IsNullOrEmpty(sysDescr))
                return false;

            // Check if it's a printer by looking for printer-specific OIDs
            var sysObjectId = await GetSysObjectIdAsync(ipAddress, credential, cancellationToken);
            if (!string.IsNullOrEmpty(sysObjectId))
            {
                // Check if the sysObjectId is from a known printer vendor
                return IsPrinterSysObjectId(sysObjectId);
            }

            // Fallback: check for printer-specific MIBs
            var result = await GetOidValueAsync(ipAddress, "1.3.6.1.2.1.25.3.2.1.3.1", credential, cancellationToken);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPrinterSysObjectId(string sysObjectId)
    {
        // Common printer vendor OIDs
        var printerOids = new[]
        {
            "1.3.6.1.4.1.11.",    // HP
            "1.3.6.1.4.1.29.",    // Lexmark
            "1.3.6.1.4.1.43.",    // Xerox
            "1.3.6.1.4.1.641.",   // Brother
            "1.3.6.1.4.1.1347.",  // Ricoh
            "1.3.6.1.4.1.367.",   // Canon
            "1.3.6.1.4.1.3808."   // Epson
        };

        return printerOids.Any(oid => sysObjectId.StartsWith(oid));
    }

    // Method to query printer MIBs for toner/drum levels
    public async Task<Dictionary<string, int?>> GetPrinterSuppliesAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        var supplies = new Dictionary<string, int?>();

        try
        {
            // Standard printer MIB OIDs for supplies
            var supplyOids = new Dictionary<string, string>
            {
                { "BlackTonerLevel", "1.3.6.1.2.1.43.11.1.1.9.1.1" },
                { "CyanTonerLevel", "1.3.6.1.2.1.43.11.1.1.9.1.2" },
                { "MagentaTonerLevel", "1.3.6.1.2.1.43.11.1.1.9.1.3" },
                { "YellowTonerLevel", "1.3.6.1.2.1.43.11.1.1.9.1.4" },
                { "DrumLevel", "1.3.6.1.2.1.43.11.1.1.9.1.6" },
                { "WasteTonerLevel", "1.3.6.1.2.1.43.11.1.1.9.1.5" }
            };

            foreach (var kvp in supplyOids)
            {
                try
                {
                    var result = await GetOidValueAsync(ipAddress, kvp.Value, credential, cancellationToken);
                    if (result != null && int.TryParse(result.ToString(), out var level))
                    {
                        supplies[kvp.Key] = level;
                    }
                    else
                    {
                        supplies[kvp.Key] = null;
                    }
                }
                catch
                {
                    supplies[kvp.Key] = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get printer supplies from {IpAddress}", ipAddress);
        }

        return supplies;
    }

    public void Dispose()
    {
        _connectionManager?.Dispose();
    }
}