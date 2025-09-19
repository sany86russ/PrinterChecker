using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces
{
    public interface ISupplyNormalizationService
    {
        Task<NormalizedSupplyData> NormalizeSupplyDataAsync(RawSnmpData rawData, Device device);
        Task<List<Supply>> MapToSuppliesAsync(NormalizedSupplyData normalizedData, int deviceId);
        Task<VendorProfile?> GetVendorProfileAsync(string manufacturer, string model);
        Task RegisterVendorProfileAsync(VendorProfile profile);
    }

    public class RawSnmpData
    {
        public Dictionary<string, object> SnmpValues { get; set; } = new();
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
        public string DeviceIpAddress { get; set; } = string.Empty;
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
    }

    public class NormalizedSupplyData
    {
        public List<NormalizedSupply> Supplies { get; set; } = new();
        public DateTime NormalizedAt { get; set; } = DateTime.UtcNow;
        public double ConfidenceScore { get; set; }
        public List<string> Warnings { get; set; } = new();
        public VendorProfile? UsedProfile { get; set; }
    }

    public class NormalizedSupply
    {
        public string Name { get; set; } = string.Empty;
        public SupplyKind Type { get; set; }
        public string Color { get; set; } = string.Empty;
        public int? CurrentLevel { get; set; }
        public int? MaxLevel { get; set; }
        public double? PercentageLevel { get; set; }
        public SupplyStatus Status { get; set; }
        public string? PartNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? InstallDate { get; set; }
        public int? PageCount { get; set; }
        public string? RawOid { get; set; }
        public object? RawValue { get; set; }
    }

    public class VendorProfile
    {
        public int Id { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? ModelPattern { get; set; } // Regex pattern for model matching
        public List<SupplyMapping> SupplyMappings { get; set; } = new();
        public Dictionary<string, string> CustomOids { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public double ConfidenceThreshold { get; set; } = 0.8;
    }

    public class SupplyMapping
    {
        public string Oid { get; set; } = string.Empty;
        public string SupplyName { get; set; } = string.Empty;
        public SupplyKind SupplyType { get; set; }
        public string Color { get; set; } = string.Empty;
        public ValueTransformation? Transformation { get; set; }
        public string? StatusOid { get; set; }
        public Dictionary<int, SupplyStatus> StatusMappings { get; set; } = new();
        public string? MaxLevelOid { get; set; }
        public string? PartNumberOid { get; set; }
        public bool IsOptional { get; set; }
    }

    public class ValueTransformation
    {
        public TransformationType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public enum TransformationType
    {
        None,
        Percentage,        // Convert to 0-100 percentage
        ReversePercentage, // 100 - value
        Scale,            // Multiply by factor
        LinearMapping,    // Map range to range
        Formula          // Custom formula
    }

    public enum SupplyStatus
    {
        Unknown = 0,
        Ok = 1,
        Low = 2,
        Critical = 3,
        Empty = 4,
        Missing = 5,
        Error = 6,
        Replacing = 7
    }
}