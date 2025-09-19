using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;

namespace TonerWatch.Infrastructure.Services;

/// <summary>
/// Service for persisting device and supply data to the database
/// </summary>
public class DevicePersistenceService
{
    private readonly TonerWatchDbContext _context;
    private readonly ILogger<DevicePersistenceService> _logger;

    public DevicePersistenceService(TonerWatchDbContext context, ILogger<DevicePersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Save or update a discovered device in the database
    /// </summary>
    public async Task<Device> SaveDeviceAsync(DiscoveredDevice discoveredDevice, int siteId = 1)
    {
        try
        {
            // Check if device already exists
            var existingDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.IpAddress == discoveredDevice.IpAddress);

            if (existingDevice != null)
            {
                // Update existing device
                existingDevice.Hostname = discoveredDevice.Hostname ?? existingDevice.Hostname;
                existingDevice.Status = DeviceStatus.Online;
                existingDevice.LastSeen = DateTime.UtcNow;
                existingDevice.UpdatedAt = DateTime.UtcNow;
                
                // Update vendor/model if we have better information
                if (!string.IsNullOrEmpty(discoveredDevice.Manufacturer) && 
                    string.IsNullOrEmpty(existingDevice.Vendor))
                {
                    existingDevice.Vendor = discoveredDevice.Manufacturer;
                }
                
                if (!string.IsNullOrEmpty(discoveredDevice.Model) && 
                    string.IsNullOrEmpty(existingDevice.Model))
                {
                    existingDevice.Model = discoveredDevice.Model;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated existing device {DeviceId} ({Hostname})", existingDevice.Id, existingDevice.Hostname);
                return existingDevice;
            }
            else
            {
                // Create new device
                var newDevice = new Device
                {
                    Hostname = discoveredDevice.Hostname ?? $"Device {discoveredDevice.IpAddress}",
                    IpAddress = discoveredDevice.IpAddress,
                    SiteId = siteId,
                    Status = DeviceStatus.Online,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Vendor = discoveredDevice.Manufacturer,
                    Model = discoveredDevice.Model,
                    Capabilities = DeviceCapabilities.None
                };

                _context.Devices.Add(newDevice);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Created new device {DeviceId} ({Hostname}) at {IpAddress}", 
                    newDevice.Id, newDevice.Hostname, newDevice.IpAddress);
                return newDevice;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device {IpAddress}", discoveredDevice.IpAddress);
            throw;
        }
    }

    /// <summary>
    /// Save supply readings for a device
    /// </summary>
    public async Task SaveSupplyReadingsAsync(int deviceId, IEnumerable<SupplyReading> readings)
    {
        try
        {
            foreach (var reading in readings)
            {
                // Check if supply already exists for this device
                var existingSupply = await _context.Supplies
                    .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.Kind == reading.Kind);

                if (existingSupply != null)
                {
                    // Update existing supply
                    existingSupply.Name = reading.Name ?? existingSupply.Name;
                    existingSupply.Percent = reading.Percent;
                    existingSupply.LevelRaw = reading.LevelRaw;
                    existingSupply.MaxRaw = reading.MaxRaw;
                    existingSupply.PartNumber = reading.PartNumber;
                    existingSupply.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new supply
                    var newSupply = new Supply
                    {
                        DeviceId = deviceId,
                        Kind = reading.Kind,
                        Name = reading.Name,
                        Percent = reading.Percent,
                        LevelRaw = reading.LevelRaw,
                        MaxRaw = reading.MaxRaw,
                        PartNumber = reading.PartNumber,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Supplies.Add(newSupply);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug("Saved {ReadingCount} supply readings for device {DeviceId}", readings.Count(), deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving supply readings for device {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Update device status
    /// </summary>
    public async Task UpdateDeviceStatusAsync(int deviceId, DeviceStatus status)
    {
        try
        {
            var device = await _context.Devices.FindAsync(deviceId);
            if (device != null)
            {
                device.Status = status;
                device.LastSeen = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated device {DeviceId} status to {Status}", deviceId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device {DeviceId} status", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Get all devices from the database
    /// </summary>
    public async Task<IEnumerable<Device>> GetAllDevicesAsync()
    {
        return await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();
    }

    /// <summary>
    /// Get device by IP address
    /// </summary>
    public async Task<Device?> GetDeviceByIpAsync(string ipAddress)
    {
        return await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .FirstOrDefaultAsync(d => d.IpAddress == ipAddress);
    }
}