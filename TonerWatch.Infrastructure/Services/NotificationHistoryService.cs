using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TonerWatch.Infrastructure.Services;

/// <summary>
/// Service for managing notification history with filtering capabilities
/// </summary>
public class NotificationHistoryService : INotificationHistoryService
{
    private readonly ILogger<NotificationHistoryService> _logger;
    private readonly TonerWatchDbContext _context;

    public NotificationHistoryService(ILogger<NotificationHistoryService> logger, TonerWatchDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Get notification history with filtering options
    /// </summary>
    public async Task<IEnumerable<NotificationHistoryEntry>> GetNotificationHistoryAsync(
        NotificationHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification history with filter");

        try
        {
            var query = _context.NotificationHistory
                .Include(n => n.Device)
                .AsQueryable();

            // Apply filters
            if (filter.DeviceId.HasValue)
            {
                query = query.Where(n => n.DeviceId == filter.DeviceId.Value);
            }

            if (filter.SiteId.HasValue)
            {
                query = query.Where(n => n.Device.SiteId == filter.SiteId.Value);
            }

            if (filter.Severity.HasValue)
            {
                query = query.Where(n => n.Severity == filter.Severity.Value);
            }

            if (filter.Category.HasValue)
            {
                query = query.Where(n => n.Category == filter.Category.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(n => n.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(n => n.CreatedAt <= filter.ToDate.Value);
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(n => 
                    n.Title.Contains(filter.SearchTerm) || 
                    n.Message.Contains(filter.SearchTerm) ||
                    n.Device.Hostname.Contains(filter.SearchTerm));
            }

            // Apply sorting
            query = query.OrderByDescending(n => n.CreatedAt);

            // Apply pagination
            if (filter.PageNumber > 0 && filter.PageSize > 0)
            {
                query = query.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize);
            }

            var results = await query.ToListAsync(cancellationToken);
            _logger.LogInformation("Retrieved {Count} notification history entries", results.Count);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification history");
            throw;
        }
    }

    /// <summary>
    /// Get notification statistics
    /// </summary>
    public async Task<NotificationStatistics> GetNotificationStatisticsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving notification statistics");

        try
        {
            var query = _context.NotificationHistory.AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(n => n.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(n => n.CreatedAt <= toDate.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            
            var severityCounts = await query
                .GroupBy(n => n.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Severity, g => g.Count, cancellationToken);

            var categoryCounts = await query
                .GroupBy(n => n.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Category, g => g.Count, cancellationToken);

            var statistics = new NotificationStatistics
            {
                TotalCount = total,
                SeverityCounts = severityCounts,
                CategoryCounts = categoryCounts,
                DateRange = new DateRange
                {
                    From = fromDate ?? DateTime.UtcNow.AddDays(-30),
                    To = toDate ?? DateTime.UtcNow
                }
            };

            _logger.LogInformation("Retrieved notification statistics: {TotalCount} total notifications", total);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification statistics");
            throw;
        }
    }

    /// <summary>
    /// Add notification to history
    /// </summary>
    public async Task AddToHistoryAsync(
        Device device,
        string title,
        string message,
        EventSeverity severity,
        AlertCategory category,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new NotificationHistoryEntry
            {
                DeviceId = device.Id,
                Title = title,
                Message = message,
                Severity = severity,
                Category = category,
                CreatedAt = DateTime.UtcNow
            };

            _context.NotificationHistory.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Added notification to history: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding notification to history: {Title}", title);
        }
    }

    /// <summary>
    /// Clean up old notification history
    /// </summary>
    public async Task CleanupHistoryAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
            var oldEntries = await _context.NotificationHistory
                .Where(n => n.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldEntries.Any())
            {
                _context.NotificationHistory.RemoveRange(oldEntries);
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Cleaned up {Count} old notification history entries", oldEntries.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up notification history");
        }
    }
}

