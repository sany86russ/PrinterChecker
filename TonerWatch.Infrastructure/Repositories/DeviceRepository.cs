using Microsoft.EntityFrameworkCore;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;

namespace TonerWatch.Infrastructure.Repositories;

/// <summary>
/// Device repository implementation
/// </summary>
public class DeviceRepository : Repository<Device>, IDeviceRepository
{
    public DeviceRepository(TonerWatchDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Device>> GetBySiteIdAsync(int siteId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.SiteId == siteId)
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task<Device?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .FirstOrDefaultAsync(d => d.Hostname == hostname, cancellationToken);
    }

    public async Task<Device?> GetByIpAddressAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .FirstOrDefaultAsync(d => d.IpAddress == ipAddress, cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetByVendorAsync(string vendor, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.Vendor == vendor)
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetByStatusAsync(DeviceStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.Status == status)
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetWithLowSuppliesAsync(double threshold = 20.0, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.Supplies.Any(s => s.Percent.HasValue && s.Percent.Value <= threshold))
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetOfflineDevicesAsync(TimeSpan offlineThreshold, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow - offlineThreshold;
        return await _dbSet
            .Where(d => d.LastSeen < cutoffTime || d.Status == DeviceStatus.Offline)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetDevicesWithSuppliesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateLastSeenAsync(int deviceId, DateTime lastSeen, CancellationToken cancellationToken = default)
    {
        await _dbSet
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeen, lastSeen), cancellationToken);
    }

    public async Task UpdateStatusAsync(int deviceId, DeviceStatus status, CancellationToken cancellationToken = default)
    {
        await _dbSet
            .Where(d => d.Id == deviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, status), cancellationToken);
    }
}