using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Device-specific repository interface
/// </summary>
public interface IDeviceRepository : IRepository<Device>
{
    Task<IEnumerable<Device>> GetBySiteIdAsync(int siteId, CancellationToken cancellationToken = default);
    Task<Device?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);
    Task<Device?> GetByIpAddressAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetByVendorAsync(string vendor, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetByStatusAsync(DeviceStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetWithLowSuppliesAsync(double threshold = 20.0, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetOfflineDevicesAsync(TimeSpan offlineThreshold, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetDevicesWithSuppliesAsync(CancellationToken cancellationToken = default);
    Task UpdateLastSeenAsync(int deviceId, DateTime lastSeen, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int deviceId, DeviceStatus status, CancellationToken cancellationToken = default);
}