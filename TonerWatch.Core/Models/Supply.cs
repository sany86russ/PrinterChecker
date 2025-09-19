namespace TonerWatch.Core.Models;

/// <summary>
/// Supply entity representing printer consumables (toner, drum, etc.)
/// </summary>
public class Supply
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public SupplyKind Kind { get; set; }
    public string? Name { get; set; } // Vendor-specific name
    public string? PartNumber { get; set; }
    public double? Percent { get; set; } // Normalized percentage (0-100)
    public int? LevelRaw { get; set; } // Raw level from device
    public int? MaxRaw { get; set; } // Raw maximum capacity from device
    public string? Unit { get; set; } // Unit of measurement (pages, ml, etc.)
    public int? NominalYield { get; set; } // Expected pages/yield from specifications
    public string? Color { get; set; } // Color code for display (#RRGGBB)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Device Device { get; set; } = null!;
    public virtual ICollection<ForecastSnapshot> ForecastSnapshots { get; set; } = new List<ForecastSnapshot>();

    /// <summary>
    /// Calculate percentage from raw values
    /// </summary>
    public double? CalculatePercent()
    {
        if (LevelRaw == null || MaxRaw == null || MaxRaw <= 0)
            return null;
            
        // Handle special SNMP values
        if (LevelRaw < 0)
            return null; // Unknown or invalid
            
        return Math.Max(0, Math.Min(100, (double)LevelRaw.Value / MaxRaw.Value * 100));
    }

    /// <summary>
    /// Get display color for the supply type
    /// </summary>
    public string GetDisplayColor()
    {
        if (!string.IsNullOrEmpty(Color))
            return Color;
            
        return Kind switch
        {
            SupplyKind.Black => "#000000",
            SupplyKind.Cyan => "#00FFFF",
            SupplyKind.Magenta => "#FF00FF",
            SupplyKind.Yellow => "#FFFF00",
            SupplyKind.Drum => "#808080",
            SupplyKind.Fuser => "#FF8C00",
            SupplyKind.TransferBelt => "#A0A0A0",
            SupplyKind.Waste => "#8B4513",
            _ => "#6B7280"
        };
    }

    /// <summary>
    /// Check if supply is in critical state
    /// </summary>
    public bool IsCritical(double threshold = 15.0)
    {
        return Percent.HasValue && Percent.Value <= threshold;
    }

    /// <summary>
    /// Check if supply is in warning state
    /// </summary>
    public bool IsWarning(double threshold = 30.0)
    {
        return Percent.HasValue && Percent.Value <= threshold && !IsCritical();
    }
}