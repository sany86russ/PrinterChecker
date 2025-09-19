namespace TonerWatch.Core.Models;

/// <summary>
/// Counter entity for tracking page counts and usage statistics
/// </summary>
public class Counter
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public int? PagesTotal { get; set; }
    public int? ColorPages { get; set; }
    public int? MonoPages { get; set; }
    public int? DuplexPages { get; set; }
    public int? ScanPages { get; set; }
    public int? FaxPages { get; set; }
    public int? CopyPages { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Device Device { get; set; } = null!;

    /// <summary>
    /// Calculate daily average for the period
    /// </summary>
    public double GetDailyAverage(int? pageCount = null)
    {
        var totalPages = pageCount ?? PagesTotal ?? 0;
        var days = Math.Max(1, (PeriodEnd - PeriodStart).TotalDays);
        return totalPages / days;
    }

    /// <summary>
    /// Get pages printed in this period
    /// </summary>
    public int GetPeriodPages()
    {
        return PagesTotal ?? 0;
    }
}