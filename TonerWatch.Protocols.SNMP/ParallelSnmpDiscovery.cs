using System.Net;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Protocols.SNMP;

/// <summary>
/// Helper class for parallel SNMP discovery operations
/// </summary>
public class ParallelSnmpDiscovery
{
    private readonly ILogger<ParallelSnmpDiscovery> _logger;
    private readonly ISnmpProtocol _snmpProtocol;
    private readonly int _maxConcurrency;

    public ParallelSnmpDiscovery(ILogger<ParallelSnmpDiscovery> logger, ISnmpProtocol snmpProtocol, int maxConcurrency = 50)
    {
        _logger = logger;
        _snmpProtocol = snmpProtocol;
        _maxConcurrency = maxConcurrency;
    }

    /// <summary>
    /// Discover multiple printers in parallel
    /// </summary>
    public async Task<List<DiscoveredDevice>> DiscoverPrintersAsync(
        IEnumerable<IPAddress> ipAddresses, 
        Credential? credential, 
        CancellationToken cancellationToken = default)
    {
        var discoveredPrinters = new List<DiscoveredDevice>();
        var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        
        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await DiscoverPrinterAsync(ip, credential, cancellationToken);
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        discoveredPrinters.AddRange(results.Where(r => r != null).Select(r => r!));

        return discoveredPrinters;
    }

    /// <summary>
    /// Discover a single printer
    /// </summary>
    private async Task<DiscoveredDevice?> DiscoverPrinterAsync(
        IPAddress ipAddress, 
        Credential? credential, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if it's a printer
            var isPrinter = await _snmpProtocol.DiscoverPrinterAsync(ipAddress, credential, cancellationToken);
            if (!isPrinter)
                return null;

            // Get basic printer info
            var deviceInfo = await _snmpProtocol.GetDeviceInfoAsync(ipAddress, credential, cancellationToken);
            if (deviceInfo == null)
                return null;

            var printer = new DiscoveredDevice
            {
                IpAddress = ipAddress.ToString(),
                Manufacturer = deviceInfo.Vendor,
                Model = deviceInfo.Model,
                SerialNumber = deviceInfo.SerialNumber,
                DiscoveryMethod = DiscoveryMethod.SubnetScan,
                EstimatedDeviceType = DeviceType.Printer,
                ConfidenceScore = 0.9,
                DiscoveredAt = DateTime.UtcNow
            };

            return printer;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to discover printer at {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// Get supply levels from multiple printers in parallel
    /// </summary>
    public async Task<Dictionary<IPAddress, IEnumerable<SupplyReading>>> GetSupplyLevelsAsync(
        IEnumerable<IPAddress> ipAddresses, 
        Credential? credential, 
        CancellationToken cancellationToken = default)
    {
        var supplyLevels = new Dictionary<IPAddress, IEnumerable<SupplyReading>>();
        var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        
        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var supplies = await _snmpProtocol.GetSupplyLevelsAsync(ip, credential, cancellationToken);
                return new { IpAddress = ip, Supplies = supplies };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        
        foreach (var result in results)
        {
            supplyLevels[result.IpAddress] = result.Supplies;
        }

        return supplyLevels;
    }
}

