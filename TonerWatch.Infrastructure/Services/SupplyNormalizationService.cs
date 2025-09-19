using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Infrastructure.Services
{
    public class SupplyNormalizationService : ISupplyNormalizationService
    {
        private readonly ILogger<SupplyNormalizationService> _logger;
        private readonly Dictionary<string, VendorProfile> _vendorProfiles;

        public SupplyNormalizationService(ILogger<SupplyNormalizationService> logger)
        {
            _logger = logger;
            _vendorProfiles = new Dictionary<string, VendorProfile>();
            InitializeBuiltInProfiles();
        }

        public async Task<NormalizedSupplyData> NormalizeSupplyDataAsync(RawSnmpData rawData, Device device)
        {
            _logger.LogDebug("Starting normalization for device {DeviceId} ({Vendor} {Model})", 
                device.Id, device.Vendor, device.Model);

            var normalizedData = new NormalizedSupplyData();
            
            try
            {
                var profile = await GetVendorProfileAsync(device.Vendor ?? "", device.Model ?? "");
                if (profile == null)
                {
                    _logger.LogWarning("No vendor profile found for {Vendor} {Model}, using generic mapping", 
                        device.Vendor, device.Model);
                    profile = GetGenericProfile();
                }

                normalizedData.UsedProfile = profile;

                foreach (var mapping in profile.SupplyMappings)
                {
                    try
                    {
                        var normalizedSupply = await ProcessSupplyMappingAsync(mapping, rawData, device);
                        if (normalizedSupply != null)
                        {
                            normalizedData.Supplies.Add(normalizedSupply);
                        }
                    }
                    catch (Exception ex)
                    {
                        var warning = $"Failed to process supply mapping {mapping.SupplyName}: {ex.Message}";
                        normalizedData.Warnings.Add(warning);
                        _logger.LogWarning(ex, "Supply mapping failed for {SupplyName}", mapping.SupplyName);
                    }
                }

                normalizedData.ConfidenceScore = CalculateConfidenceScore(normalizedData, profile);
                
                _logger.LogInformation("Normalization completed for device {DeviceId}. Found {SupplyCount} supplies", 
                    device.Id, normalizedData.Supplies.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during supply normalization for device {DeviceId}", device.Id);
                normalizedData.Warnings.Add($"Critical normalization error: {ex.Message}");
                normalizedData.ConfidenceScore = 0.0;
            }

            return normalizedData;
        }

        private async Task<NormalizedSupply?> ProcessSupplyMappingAsync(
            SupplyMapping mapping, 
            RawSnmpData rawData, 
            Device device)
        {
            if (!rawData.SnmpValues.ContainsKey(mapping.Oid))
            {
                if (!mapping.IsOptional)
                {
                    _logger.LogDebug("Required OID {Oid} not found for supply {SupplyName}", 
                        mapping.Oid, mapping.SupplyName);
                }
                return null;
            }

            var rawValue = rawData.SnmpValues[mapping.Oid];
            var normalizedSupply = new NormalizedSupply
            {
                Name = mapping.SupplyName,
                Type = mapping.SupplyType,
                Color = mapping.Color,
                RawOid = mapping.Oid,
                RawValue = rawValue
            };

            if (int.TryParse(rawValue?.ToString(), out var intValue))
            {
                normalizedSupply.CurrentLevel = intValue;
                
                if (intValue >= 0 && intValue <= 100)
                {
                    normalizedSupply.PercentageLevel = intValue;
                }
            }

            normalizedSupply.Status = GetSupplyStatus(normalizedSupply);
            return normalizedSupply;
        }

        private static SupplyStatus GetSupplyStatus(NormalizedSupply supply)
        {
            if (supply.PercentageLevel.HasValue)
            {
                var percentage = supply.PercentageLevel.Value;
                return percentage switch
                {
                    <= 0 => SupplyStatus.Empty,
                    <= 10 => SupplyStatus.Critical,
                    <= 25 => SupplyStatus.Low,
                    _ => SupplyStatus.Ok
                };
            }

            return SupplyStatus.Unknown;
        }

        private static double CalculateConfidenceScore(NormalizedSupplyData normalizedData, VendorProfile profile)
        {
            if (normalizedData.Supplies.Count == 0) return 0.0;

            double score = profile.ConfidenceThreshold * 0.5;
            
            var successfulMappings = normalizedData.Supplies.Count;
            var totalMappings = profile.SupplyMappings.Count;
            if (totalMappings > 0)
            {
                score += (double)successfulMappings / totalMappings * 0.5;
            }

            return Math.Min(1.0, score);
        }

        public async Task<List<Supply>> MapToSuppliesAsync(NormalizedSupplyData normalizedData, int deviceId)
        {
            var supplies = new List<Supply>();

            foreach (var normalizedSupply in normalizedData.Supplies)
            {
                var supply = new Supply
                {
                    DeviceId = deviceId,
                    Name = normalizedSupply.Name,
                    Kind = normalizedSupply.Type,
                    Color = normalizedSupply.Color,
                    LevelRaw = normalizedSupply.CurrentLevel,
                    MaxRaw = normalizedSupply.MaxLevel,
                    Percent = normalizedSupply.PercentageLevel,
                    PartNumber = normalizedSupply.PartNumber,
                    UpdatedAt = DateTime.UtcNow
                };

                supplies.Add(supply);
            }

            return supplies;
        }

        public async Task<VendorProfile?> GetVendorProfileAsync(string vendor, string model)
        {
            var key = $"{vendor}:{model}".ToLowerInvariant();
            
            if (_vendorProfiles.TryGetValue(key, out var exactProfile))
            {
                return exactProfile;
            }

            var vendorLower = vendor.ToLowerInvariant();
            foreach (var profile in _vendorProfiles.Values)
            {
                if (profile.Manufacturer.Equals(vendorLower, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(profile.ModelPattern))
                    {
                        try
                        {
                            if (Regex.IsMatch(model, profile.ModelPattern, RegexOptions.IgnoreCase))
                            {
                                return profile;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Invalid regex pattern in vendor profile: {Pattern}", profile.ModelPattern);
                        }
                    }
                }
            }

            return null;
        }

        public async Task RegisterVendorProfileAsync(VendorProfile profile)
        {
            var key = $"{profile.Manufacturer}:{profile.Model}".ToLowerInvariant();
            _vendorProfiles[key] = profile;
            
            _logger.LogInformation("Registered vendor profile for {Manufacturer} {Model}", 
                profile.Manufacturer, profile.Model);
        }

        private VendorProfile GetGenericProfile()
        {
            return new VendorProfile
            {
                Manufacturer = "Generic",
                Model = "Unknown",
                ConfidenceThreshold = 0.3,
                SupplyMappings = new List<SupplyMapping>
                {
                    new()
                    {
                        Oid = "1.3.6.1.2.1.43.11.1.1.9.1.1",
                        SupplyName = "Black Toner",
                        SupplyType = SupplyKind.Black,
                        Color = "Black",
                        IsOptional = true
                    }
                }
            };
        }

        private void InitializeBuiltInProfiles()
        {
            var hpProfile = new VendorProfile
            {
                Manufacturer = "HP",
                Model = "LaserJet",
                ModelPattern = @"LaserJet.*",
                ConfidenceThreshold = 0.9,
                SupplyMappings = new List<SupplyMapping>
                {
                    new()
                    {
                        Oid = "1.3.6.1.2.1.43.11.1.1.9.1.1",
                        SupplyName = "Black Toner",
                        SupplyType = SupplyKind.Black,
                        Color = "Black"
                    }
                }
            };

            var canonProfile = new VendorProfile
            {
                Manufacturer = "Canon",
                Model = "imageRUNNER",
                ModelPattern = @"imageRUNNER.*",
                ConfidenceThreshold = 0.9,
                SupplyMappings = new List<SupplyMapping>
                {
                    new()
                    {
                        Oid = "1.3.6.1.2.1.43.11.1.1.9.1.1",
                        SupplyName = "Black Toner",
                        SupplyType = SupplyKind.Black,
                        Color = "Black"
                    }
                }
            };

            var profiles = new[] { hpProfile, canonProfile };
            foreach (var profile in profiles)
            {
                var key = $"{profile.Manufacturer}:{profile.Model}".ToLowerInvariant();
                _vendorProfiles[key] = profile;
            }

            _logger.LogInformation("Initialized {ProfileCount} built-in vendor profiles", profiles.Length);
        }
    }
}