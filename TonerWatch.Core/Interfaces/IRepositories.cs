using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Supply-specific repository interface
/// </summary>
public interface ISupplyRepository : IRepository<Supply>
{
    Task<IEnumerable<Supply>> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supply>> GetBySupplyKindAsync(SupplyKind kind, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supply>> GetLowSuppliesAsync(double threshold = 20.0, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supply>> GetCriticalSuppliesAsync(double threshold = 10.0, CancellationToken cancellationToken = default);
    Task<Supply?> GetSupplyByDeviceAndKindAsync(int deviceId, SupplyKind kind, CancellationToken cancellationToken = default);
    Task UpdateSupplyLevelAsync(int deviceId, SupplyKind kind, double? percent, int? levelRaw, int? maxRaw, CancellationToken cancellationToken = default);
    Task<IEnumerable<Supply>> GetSuppliesUpdatedAfterAsync(DateTime after, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event-specific repository interface
/// </summary>
public interface IEventRepository : IRepository<Event>
{
    Task<IEnumerable<Event>> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetBySeverityAsync(EventSeverity severity, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetActiveEventsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetRecentEventsAsync(TimeSpan timeSpan, CancellationToken cancellationToken = default);
    Task<Event?> GetByFingerprintAsync(string fingerprint, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<int> GetActiveEventCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetCriticalEventCountAsync(CancellationToken cancellationToken = default);
    Task AcknowledgeEventAsync(int eventId, string acknowledgedBy, CancellationToken cancellationToken = default);
    Task ResolveEventAsync(int eventId, string? resolution = null, CancellationToken cancellationToken = default);
    Task MuteEventAsync(int eventId, TimeSpan duration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Site-specific repository interface
/// </summary>
public interface ISiteRepository : IRepository<Site>
{
    Task<Site?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Site>> GetSitesWithDevicesAsync(CancellationToken cancellationToken = default);
    Task<int> GetDeviceCountBySiteAsync(int siteId, CancellationToken cancellationToken = default);
}