namespace TonerWatch.Core.Models;

/// <summary>
/// Forecast snapshot for supply level predictions
/// </summary>
public class ForecastSnapshot
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public SupplyKind SupplyKind { get; set; }
    public int? DaysLeft { get; set; }
    public double? Confidence { get; set; } // 0.0 to 1.0
    public double? DailyUsage { get; set; } // Estimated daily usage
    public double? UsageVariance { get; set; } // Variance in usage pattern
    public string? Model { get; set; } // Forecasting model used (EWMA, Linear, etc.)
    public string? Parameters { get; set; } // JSON parameters used for the forecast
    public DateTime At { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Device Device { get; set; } = null!;
    public virtual Supply? Supply { get; set; }

    /// <summary>
    /// Get confidence percentage
    /// </summary>
    public double GetConfidencePercent()
    {
        return (Confidence ?? 0.0) * 100.0;
    }

    /// <summary>
    /// Get forecast accuracy level
    /// </summary>
    public string GetAccuracyLevel()
    {
        return Confidence switch
        {
            >= 0.8 => "High",
            >= 0.6 => "Medium",
            >= 0.4 => "Low",
            _ => "Very Low"
        };
    }

    /// <summary>
    /// Check if forecast is reliable
    /// </summary>
    public bool IsReliable(double minimumConfidence = 0.5)
    {
        return Confidence >= minimumConfidence && DaysLeft.HasValue;
    }
}