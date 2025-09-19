using TonerWatch.Core.Models;

namespace TonerWatch.Core.Extensions;

/// <summary>
/// Supply-related extensions
/// </summary>
public static class SupplyExtensions
{
    /// <summary>
    /// Get CSS class for supply level
    /// </summary>
    public static string GetCssClass(this Supply supply)
    {
        if (!supply.Percent.HasValue)
            return "supply-unknown";

        return supply.Percent.Value switch
        {
            <= 10 => "supply-critical",
            <= 25 => "supply-warning",
            <= 50 => "supply-low",
            _ => "supply-ok"
        };
    }

    /// <summary>
    /// Get display name for supply kind
    /// </summary>
    public static string GetDisplayName(this SupplyKind kind)
    {
        return kind switch
        {
            SupplyKind.Black => "Black Toner",
            SupplyKind.Cyan => "Cyan Toner",
            SupplyKind.Magenta => "Magenta Toner",
            SupplyKind.Yellow => "Yellow Toner",
            SupplyKind.Drum => "Drum Unit",
            SupplyKind.Fuser => "Fuser Unit",
            SupplyKind.TransferBelt => "Transfer Belt",
            SupplyKind.Waste => "Waste Toner",
            SupplyKind.MaintenanceKit => "Maintenance Kit",
            SupplyKind.PhotoconductorUnit => "Photoconductor Unit",
            SupplyKind.DeveloperUnit => "Developer Unit",
            SupplyKind.TransferUnit => "Transfer Unit",
            SupplyKind.CleaningUnit => "Cleaning Unit",
            SupplyKind.TonerCollection => "Toner Collection",
            _ => "Unknown Supply"
        };
    }

    /// <summary>
    /// Get short display name for supply kind
    /// </summary>
    public static string GetShortName(this SupplyKind kind)
    {
        return kind switch
        {
            SupplyKind.Black => "K",
            SupplyKind.Cyan => "C",
            SupplyKind.Magenta => "M",
            SupplyKind.Yellow => "Y",
            SupplyKind.Drum => "Drum",
            SupplyKind.Fuser => "Fuser",
            SupplyKind.TransferBelt => "Belt",
            SupplyKind.Waste => "Waste",
            SupplyKind.MaintenanceKit => "Maint",
            _ => kind.ToString()
        };
    }

    /// <summary>
    /// Check if supply is color toner
    /// </summary>
    public static bool IsColorToner(this SupplyKind kind)
    {
        return kind is SupplyKind.Cyan or SupplyKind.Magenta or SupplyKind.Yellow;
    }

    /// <summary>
    /// Check if supply is toner
    /// </summary>
    public static bool IsToner(this SupplyKind kind)
    {
        return kind is SupplyKind.Black or SupplyKind.Cyan or SupplyKind.Magenta or SupplyKind.Yellow;
    }

    /// <summary>
    /// Get priority for sorting supplies
    /// </summary>
    public static int GetSortPriority(this SupplyKind kind)
    {
        return kind switch
        {
            SupplyKind.Black => 1,
            SupplyKind.Cyan => 2,
            SupplyKind.Magenta => 3,
            SupplyKind.Yellow => 4,
            SupplyKind.Drum => 5,
            SupplyKind.Fuser => 6,
            SupplyKind.TransferBelt => 7,
            SupplyKind.Waste => 8,
            _ => 9
        };
    }
}

/// <summary>
/// Device-related extensions
/// </summary>
public static class DeviceExtensions
{
    /// <summary>
    /// Get display name for device status
    /// </summary>
    public static string GetDisplayName(this DeviceStatus status)
    {
        return status switch
        {
            DeviceStatus.Online => "Online",
            DeviceStatus.Offline => "Offline",
            DeviceStatus.Warning => "Warning",
            DeviceStatus.Error => "Error",
            DeviceStatus.Maintenance => "Maintenance",
            DeviceStatus.StandBy => "Standby",
            DeviceStatus.Processing => "Processing",
            DeviceStatus.Stopped => "Stopped",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Get CSS class for device status
    /// </summary>
    public static string GetCssClass(this DeviceStatus status)
    {
        return status switch
        {
            DeviceStatus.Online => "status-online",
            DeviceStatus.Offline => "status-offline",
            DeviceStatus.Warning => "status-warning",
            DeviceStatus.Error => "status-error",
            DeviceStatus.Maintenance => "status-maintenance",
            DeviceStatus.StandBy => "status-standby",
            DeviceStatus.Processing => "status-processing",
            DeviceStatus.Stopped => "status-stopped",
            _ => "status-unknown"
        };
    }

    /// <summary>
    /// Check if device is healthy
    /// </summary>
    public static bool IsHealthy(this Device device)
    {
        return device.Status is DeviceStatus.Online or DeviceStatus.StandBy or DeviceStatus.Processing;
    }

    /// <summary>
    /// Get vendor icon class
    /// </summary>
    public static string GetVendorIcon(this Device device)
    {
        return device.Vendor?.ToLowerInvariant() switch
        {
            "hp" or "hewlett-packard" => "vendor-hp",
            "canon" => "vendor-canon",
            "brother" => "vendor-brother",
            "kyocera" => "vendor-kyocera",
            "ricoh" => "vendor-ricoh",
            "xerox" => "vendor-xerox",
            "epson" => "vendor-epson",
            "lexmark" => "vendor-lexmark",
            _ => "vendor-generic"
        };
    }
}