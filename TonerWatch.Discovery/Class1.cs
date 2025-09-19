﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Microsoft.Extensions.Logging;
using System.DirectoryServices;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using TonerWatch.Core.Extensions;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Protocols.SNMP;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace TonerWatch.Discovery;

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly ISnmpProtocol? _snmpProtocol;
    private readonly SemaphoreSlim _semaphore;
    private readonly IMemoryCache _scanCache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    private readonly Dictionary<string, DateTime> _lastScanTimes = new();
    private readonly ConcurrentDictionary<string, DateTime> _deviceLastSeen = new();

    public DeviceDiscoveryService(ILogger<DeviceDiscoveryService> logger, ISnmpProtocol? snmpProtocol = null)
    {
        _logger = logger;
        _snmpProtocol = snmpProtocol;
        _semaphore = new SemaphoreSlim(50); // Default max concurrent operations
        _scanCache = new MemoryCache(new MemoryCacheOptions());
    }

    public async Task<IEnumerable<DiscoveredDevice>> DiscoverDevicesAsync(
        DiscoverySettings settings, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting device discovery with settings: SubnetRange={SubnetRange}, UseAD={UseAD}", 
            settings.SubnetRange, settings.UseActiveDirectory);

        var allDevices = new List<DiscoveredDevice>();
        var tasks = new List<Task<IEnumerable<DiscoveredDevice>>>();

        // Subnet scanning
        if (settings.UseSubnetScan)
        {
            tasks.Add(ScanSubnetAsync(settings.SubnetRange, settings.ScanTimeout, settings.MaxConcurrentScans, settings, cancellationToken));
        }

        // Active Directory discovery
        if (settings.UseActiveDirectory)
        {
            tasks.Add(DiscoverFromActiveDirectoryAsync(cancellationToken));
        }

        // WMI discovery
        if (settings.UseWmi)
        {
            tasks.Add(DiscoverFromWmiAsync(cancellationToken));
        }

        try
        {
            var results = await Task.WhenAll(tasks);
            foreach (var deviceList in results)
            {
                allDevices.AddRange(deviceList);
            }

            // Update last seen times for all discovered devices
            foreach (var device in allDevices)
            {
                _deviceLastSeen[device.IpAddress] = DateTime.UtcNow;
            }

            // Deduplicate devices by IP address
            var uniqueDevices = allDevices
                .GroupBy(d => d.IpAddress)
                .Select(g => g.OrderByDescending(d => d.ConfidenceScore).First())
                .ToList();

            // If SNMP is enabled and we have an SNMP protocol, enhance device information
            if (settings.EnableSNMPFingerprinting && _snmpProtocol != null)
            {
                await EnhanceDevicesWithSnmpAsync(uniqueDevices, cancellationToken);
            }

            _logger.LogInformation("Discovery completed. Found {TotalDevices} total, {UniqueDevices} unique devices", 
                allDevices.Count, uniqueDevices.Count);

            // Cache the results
            var cacheKey = GenerateCacheKey(settings.SubnetRange ?? "default", settings);
            _scanCache.Set(cacheKey, uniqueDevices, _cacheExpiration);

            return uniqueDevices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device discovery");
            throw;
        }
    }

    public async Task<IEnumerable<DiscoveredDevice>> ScanSubnetAsync(
        string subnet, 
        TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        // Create default settings for backward compatibility
        var defaultSettings = new DiscoverySettings
        {
            SubnetRange = subnet,
            ScanTimeout = timeout,
            MaxConcurrentScans = 50,
            ScanRetries = 1,
            ScanRetryDelayMs = 1000
        };
        
        return await ScanSubnetAsync(subnet, timeout, 50, defaultSettings, cancellationToken);
    }

    public async Task<IEnumerable<DiscoveredDevice>> ScanSubnetAsync(
        string subnet, 
        TimeSpan timeout,
        int maxConcurrentScans,
        CancellationToken cancellationToken = default)
    {
        // Create default settings for backward compatibility
        var defaultSettings = new DiscoverySettings
        {
            SubnetRange = subnet,
            ScanTimeout = timeout,
            MaxConcurrentScans = maxConcurrentScans,
            ScanRetries = 1,
            ScanRetryDelayMs = 1000
        };
        
        return await ScanSubnetAsync(subnet, timeout, maxConcurrentScans, defaultSettings, cancellationToken);
    }

    // New enhanced method with full settings support
    public async Task<IEnumerable<DiscoveredDevice>> ScanSubnetAsync(
        string subnet, 
        TimeSpan timeout,
        int maxConcurrentScans,
        DiscoverySettings settings,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting subnet scan for {Subnet} with timeout {Timeout} and max concurrency {MaxConcurrentScans}", 
            subnet, timeout, maxConcurrentScans);

        // Generate cache key based on subnet and settings
        var cacheKey = GenerateCacheKey(subnet, settings);

        // Check if we have cached results
        if (_scanCache.TryGetValue(cacheKey, out List<DiscoveredDevice> cachedDevices))
        {
            _logger.LogInformation("Returning cached scan results for {Subnet}", subnet);
            return cachedDevices;
        }

        var devices = new List<DiscoveredDevice>();
        var ipAddresses = IpRangeParser.ParseMultipleRanges(subnet).ToList();

        // Check for incremental scanning - if we've scanned this subnet recently, 
        // only scan devices that weren't recently checked
        var incrementalIps = GetIpsForIncrementalScan(subnet, ipAddresses, settings);

        // For very large networks, we might want to process in batches to avoid memory issues
        const int batchSize = 1000;
        var batches = incrementalIps
            .Select((ip, index) => new { ip, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.ip).ToList())
            .ToList();

        _logger.LogInformation("Processing {IpAddressCount} IP addresses in {BatchCount} batches ({TotalCount} total IPs)", 
            incrementalIps.Count, batches.Count, ipAddresses.Count);

        var totalBatches = batches.Count;
        var completedBatches = 0;

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Report progress
            completedBatches++;
            _logger.LogInformation("Processing batch {CompletedBatches}/{TotalBatches} for subnet {Subnet}", 
                completedBatches, totalBatches, subnet);

            var semaphore = new SemaphoreSlim(maxConcurrentScans);
            var tasks = batch.Select(async ip =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ScanSingleDeviceWithRetryAsync(ip.ToString(), timeout, settings, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            devices.AddRange(results.Where(d => d != null)!);

            _logger.LogInformation("Completed batch with {DeviceCount} responsive devices", 
                results.Count(d => d != null));
        }

        _logger.LogInformation("Subnet scan completed. Found {DeviceCount} responsive devices", devices.Count);
        
        // Cache the results
        _scanCache.Set(cacheKey, devices, _cacheExpiration);
        
        // Update last scan time
        _lastScanTimes[subnet] = DateTime.UtcNow;
        
        return devices;
    }

    /// <summary>
    /// Determine which IPs to scan based on incremental scanning settings
    /// </summary>
    private List<IPAddress> GetIpsForIncrementalScan(string subnet, List<IPAddress> allIps, DiscoverySettings settings)
    {
        // If incremental scanning is disabled or this is the first scan, scan all IPs
        if (!_lastScanTimes.ContainsKey(subnet) || !settings.EnableIncrementalScanning)
        {
            return allIps;
        }

        var lastScan = _lastScanTimes[subnet];
        var timeSinceLastScan = DateTime.UtcNow - lastScan;

        // If less than 5 minutes since last scan, only scan devices that haven't responded recently
        if (timeSinceLastScan.TotalMinutes < 5)
        {
            // Only scan devices that haven't been seen in the last 5 minutes
            return allIps.Where(ip => 
            {
                var ipStr = ip.ToString();
                return !_deviceLastSeen.ContainsKey(ipStr) || 
                       (DateTime.UtcNow - _deviceLastSeen[ipStr]).TotalMinutes > 5;
            }).ToList();
        }

        // Otherwise, scan all IPs
        return allIps;
    }

    // Helper method to generate cache key
    private string GenerateCacheKey(string subnet, DiscoverySettings settings)
    {
        // Create a unique key based on subnet and relevant settings
        return $"scan_{subnet}_{settings.ScanTimeout.TotalSeconds}_{settings.MaxConcurrentScans}_{settings.ScanRetries}";
    }

    private async Task<DiscoveredDevice?> ScanSingleDeviceWithRetryAsync(
        string ipAddress, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        // For backward compatibility, create default settings
        var defaultSettings = new DiscoverySettings
        {
            ScanRetries = 1,
            ScanRetryDelayMs = 1000
        };
        
        return await ScanSingleDeviceWithRetryAsync(ipAddress, timeout, defaultSettings, cancellationToken);
    }

    private async Task<DiscoveredDevice?> ScanSingleDeviceWithRetryAsync(
        string ipAddress, 
        TimeSpan timeout, 
        DiscoverySettings settings,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= settings.ScanRetries; attempt++)
        {
            try
            {
                var result = await ScanSingleDeviceAsync(ipAddress, timeout, cancellationToken);
                if (result != null || attempt == settings.ScanRetries)
                {
                    return result;
                }
                
                // Wait before retrying
                if (attempt < settings.ScanRetries)
                {
                    await Task.Delay(settings.ScanRetryDelayMs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to scan {IpAddress} on attempt {Attempt}: {Error}", ipAddress, attempt + 1, ex.Message);
                
                // If this is the last attempt, return null
                if (attempt == settings.ScanRetries)
                {
                    return null;
                }
                
                // Wait before retrying
                await Task.Delay(settings.ScanRetryDelayMs, cancellationToken);
            }
        }
        
        return null;
    }

    private async Task<DiscoveredDevice?> ScanSingleDeviceAsync(
        string ipAddress, 
        TimeSpan timeout, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            
            // Implement intelligent timeout - start with a shorter timeout and increase if needed
            var initialTimeout = Math.Min((int)timeout.TotalMilliseconds / 2, 1000); // Start with 1 second or half of timeout
            var reply = await ping.SendPingAsync(IPAddress.Parse(ipAddress), initialTimeout);

            // If the initial ping failed but we have time for a retry, try with full timeout
            if (reply.Status != IPStatus.Success && initialTimeout < (int)timeout.TotalMilliseconds)
            {
                reply = await ping.SendPingAsync(IPAddress.Parse(ipAddress), (int)timeout.TotalMilliseconds - initialTimeout);
            }

            if (reply.Status == IPStatus.Success)
            {
                var device = new DiscoveredDevice
                {
                    IpAddress = ipAddress,
                    DiscoveryMethod = DiscoveryMethod.SubnetScan,
                    DiscoveredAt = DateTime.UtcNow
                };

                // Try to get hostname
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(IPAddress.Parse(ipAddress));
                    device.Hostname = hostEntry.HostName;
                }
                catch
                {
                    // Hostname resolution failed, continue without it
                }

                // Test common printer ports with adaptive timeouts
                var portsToScan = new[] { 161, 80, 443, 9100, 515 };
                var portTasks = portsToScan.Select(async port =>
                {
                    // Use shorter timeout for initial ports, longer for web ports
                    var portTimeout = port == 80 || port == 443 ? 
                        TimeSpan.FromSeconds(3) : 
                        TimeSpan.FromSeconds(1);
                    
                    var isOpen = await TestPortAsync(ipAddress, port, portTimeout);
                    return new { Port = port, IsOpen = isOpen };
                });

                var portResults = await Task.WhenAll(portTasks);
                device.OpenPorts = portResults.Where(p => p.IsOpen).Select(p => p.Port).ToList();

                // Determine if it's likely a printer
                device.EstimatedDeviceType = EstimateDeviceType(device.OpenPorts, device.Hostname);
                device.ConfidenceScore = CalculateConfidenceScore(device);
                device.SupportsSnmp = device.OpenPorts.Contains(161);

                if (device.EstimatedDeviceType == DeviceType.Printer || 
                    device.EstimatedDeviceType == DeviceType.MultiFunctionDevice)
                {
                    return device;
                }
            }
            else
            {
                // Log timeout information for debugging
                _logger.LogDebug("Device {IpAddress} not responding to ping: {Status}", ipAddress, reply.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to scan {IpAddress}: {Error}", ipAddress, ex.Message);
        }

        return null;
    }

    private static DeviceType EstimateDeviceType(List<int> openPorts, string? hostname)
    {
        // Check for printer-specific ports
        if (openPorts.Contains(9100) || openPorts.Contains(515)) // Raw printing, LPR
        {
            return DeviceType.Printer;
        }

        // Check hostname for printer indicators
        if (!string.IsNullOrEmpty(hostname))
        {
            var lowerHostname = hostname.ToLowerInvariant();
            if (lowerHostname.Contains("printer") || lowerHostname.Contains("hp") || 
                lowerHostname.Contains("canon") || lowerHostname.Contains("epson") ||
                lowerHostname.Contains("brother") || lowerHostname.Contains("xerox") ||
                lowerHostname.Contains("lexmark") || lowerHostname.Contains("ricoh"))
            {
                return DeviceType.Printer;
            }
        }

        // Has SNMP and HTTP - likely a network device, possibly MFD
        if (openPorts.Contains(161) && (openPorts.Contains(80) || openPorts.Contains(443)))
        {
            return DeviceType.MultiFunctionDevice;
        }

        return DeviceType.Unknown;
    }

    private static double CalculateConfidenceScore(DiscoveredDevice device)
    {
        double score = 0.0;

        // Port-based scoring
        if (device.OpenPorts.Contains(9100)) score += 0.4; // Raw printing
        if (device.OpenPorts.Contains(515)) score += 0.3;  // LPR
        if (device.OpenPorts.Contains(161)) score += 0.2;  // SNMP
        if (device.OpenPorts.Contains(80) || device.OpenPorts.Contains(443)) score += 0.1; // HTTP/HTTPS

        // Hostname-based scoring
        if (!string.IsNullOrEmpty(device.Hostname))
        {
            var hostname = device.Hostname.ToLowerInvariant();
            if (hostname.Contains("printer")) score += 0.3;
            if (hostname.Contains("hp") || hostname.Contains("canon") || hostname.Contains("epson")) score += 0.2;
        }

        return Math.Min(1.0, score);
    }

    private static async Task<bool> TestPortAsync(string ipAddress, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(IPAddress.Parse(ipAddress), port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout));
            
            return completedTask == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<DiscoveredDevice>> DiscoverFromActiveDirectoryAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = new List<DiscoveredDevice>();

        try
        {
            _logger.LogInformation("Starting Active Directory printer discovery");

            using var searcher = new DirectorySearcher()
            {
                Filter = "(objectClass=printQueue)",
                PropertiesToLoad = { "printerName", "serverName", "portName", "driverName", "location" }
            };

            var results = searcher.FindAll();
            foreach (SearchResult result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var device = new DiscoveredDevice
                {
                    DiscoveryMethod = DiscoveryMethod.ActiveDirectory,
                    EstimatedDeviceType = DeviceType.Printer,
                    ConfidenceScore = 0.8, // High confidence from AD
                    DiscoveredAt = DateTime.UtcNow
                };

                // Extract printer information
                if (result.Properties["printerName"].Count > 0)
                {
                    device.Hostname = result.Properties["printerName"][0]?.ToString();
                }

                if (result.Properties["portName"].Count > 0)
                {
                    var portName = result.Properties["portName"][0]?.ToString();
                    if (!string.IsNullOrEmpty(portName) && IPAddress.TryParse(portName, out var ip))
                    {
                        device.IpAddress = ip.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(device.IpAddress))
                {
                    devices.Add(device);
                }
            }

            _logger.LogInformation("Active Directory discovery found {DeviceCount} printers", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active Directory discovery failed");
        }

        return devices;
    }

    public async Task<IEnumerable<DiscoveredDevice>> DiscoverFromWmiAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = new List<DiscoveredDevice>();

        try
        {
            _logger.LogInformation("Starting WMI printer discovery");

            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                var printers = searcher.Get();

                foreach (ManagementObject printer in printers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var device = new DiscoveredDevice
                    {
                        DiscoveryMethod = DiscoveryMethod.WindowsManagementInstrumentation,
                        EstimatedDeviceType = DeviceType.Printer,
                        ConfidenceScore = 0.7,
                        DiscoveredAt = DateTime.UtcNow
                    };

                    device.Hostname = printer["Name"]?.ToString();
                    var portName = printer["PortName"]?.ToString();

                    if (!string.IsNullOrEmpty(portName) && IPAddress.TryParse(portName, out var ip))
                    {
                        device.IpAddress = ip.ToString();
                        devices.Add(device);
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("WMI discovery found {DeviceCount} printers", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI discovery failed");
        }

        return devices;
    }

    public async Task<bool> TestDeviceConnectivityAsync(
        string ipAddress, 
        int port = 161, 
        TimeSpan? timeout = null)
    {
        var testTimeout = timeout ?? TimeSpan.FromSeconds(5);
        
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(IPAddress.Parse(ipAddress), (int)testTimeout.TotalMilliseconds);
            
            if (reply.Status != IPStatus.Success)
            {
                return false;
            }

            return await TestPortAsync(ipAddress, port, testTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Connectivity test failed for {IpAddress}:{Port} - {Error}", 
                ipAddress, port, ex.Message);
            return false;
        }
    }

    private static List<string> GetIpAddressesFromSubnet(string subnet)
    {
        // Use the new IP range parser that supports multiple formats
        return IpRangeParser.ParseMultipleRanges(subnet).Select(ip => ip.ToString()).ToList();
    }

    private async Task EnhanceDevicesWithSnmpAsync(List<DiscoveredDevice> devices, CancellationToken cancellationToken)
    {
        if (_snmpProtocol == null) return;

        try
        {
            _logger.LogInformation("Enhancing {DeviceCount} devices with SNMP fingerprinting", devices.Count);

            // Use parallel SNMP discovery for better performance
            var parallelDiscovery = new ParallelSnmpDiscovery(
                (ILogger<ParallelSnmpDiscovery>)(ILogger)_logger, 
                _snmpProtocol);
            
            // Filter devices that support SNMP
            var snmpDevices = devices
                .Where(d => d.SupportsSnmp && !string.IsNullOrEmpty(d.IpAddress) && IPAddress.TryParse(d.IpAddress, out _))
                .ToList();

            if (!snmpDevices.Any()) return;

            // Convert to IP addresses
            var ipAddresses = snmpDevices.Select(d => IPAddress.Parse(d.IpAddress!)).ToList();
            
            // Use default credential for now - in a real implementation this would come from settings
            var credential = new Credential
            {
                Type = CredentialType.SNMPv2c,
                Name = "default",
                SecretRef = "public"
            };

            // Discover printers in parallel
            var discoveredPrinters = await parallelDiscovery.DiscoverPrintersAsync(ipAddresses, credential, cancellationToken);
            
            // Update device information
            foreach (var printer in discoveredPrinters)
            {
                var device = devices.FirstOrDefault(d => d.IpAddress == printer.IpAddress);
                if (device != null)
                {
                    device.Manufacturer = printer.Manufacturer;
                    device.Model = printer.Model;
                    device.SerialNumber = printer.SerialNumber;
                    device.EstimatedDeviceType = DeviceType.Printer;
                    device.ConfidenceScore = Math.Min(1.0, device.ConfidenceScore + 0.3); // Increase confidence
                }
            }

            _logger.LogInformation("Enhanced {PrinterCount} devices with SNMP information", discoveredPrinters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance devices with SNMP fingerprinting");
        }
    }

}
