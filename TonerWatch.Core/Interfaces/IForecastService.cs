using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Forecast parameters for supply predictions
/// </summary>
public class ForecastParameters
{
    public double EwmaAlpha { get; set; } = 0.3;
    public int MinimumDataPoints { get; set; } = 7;
    public TimeSpan HistoryWindow { get; set; } = TimeSpan.FromDays(30);
    public bool ConsiderWeeklySeasonality { get; set; } = true;
    public double ConfidenceLevel { get; set; } = 0.8;
    public int MaxDaysToPredict { get; set; } = 365;
}

/// <summary>
/// Forecast result for a supply
/// </summary>
public class ForecastResult
{
    public SupplyKind SupplyKind { get; set; }
    public int? DaysLeft { get; set; }
    public double? Confidence { get; set; }
    public double? DailyUsage { get; set; }
    public double? UsageVariance { get; set; }
    public string Model { get; set; } = "EWMA";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service interface for supply level forecasting
/// </summary>
public interface IForecastService
{
    /// <summary>
    /// Generate forecast for device supplies
    /// </summary>
    Task<IEnumerable<ForecastResult>> ForecastDeviceSuppliesAsync(int deviceId, ForecastParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate forecast for specific supply
    /// </summary>
    Task<ForecastResult?> ForecastSupplyAsync(int deviceId, SupplyKind supplyKind, ForecastParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate daily usage rate using EWMA
    /// </summary>
    Task<double?> CalculateDailyUsageAsync(int deviceId, SupplyKind supplyKind, ForecastParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update forecast snapshots for all devices
    /// </summary>
    Task UpdateAllForecastsAsync(ForecastParameters? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get confidence band for forecast
    /// </summary>
    (int? lowerBound, int? upperBound) GetConfidenceBand(ForecastResult forecast, double confidenceLevel = 0.8);
}