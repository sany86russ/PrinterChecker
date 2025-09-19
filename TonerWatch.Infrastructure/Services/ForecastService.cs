using Microsoft.Extensions.Logging;
using System.Data;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TonerWatch.Infrastructure.Services
{
    public class ForecastService : IForecastService
    {
        private readonly ILogger<ForecastService> _logger;
        private readonly TonerWatchDbContext _context;

        public ForecastService(ILogger<ForecastService> logger, TonerWatchDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IEnumerable<ForecastResult>> ForecastDeviceSuppliesAsync(
            int deviceId, 
            ForecastParameters? parameters = null, 
            CancellationToken cancellationToken = default)
        {
            parameters ??= new ForecastParameters();
            
            _logger.LogDebug("Generating forecast for device {DeviceId}", deviceId);

            var results = new List<ForecastResult>();
            
            try
            {
                // Get the device supplies
                var supplies = await _context.Supplies
                    .Where(s => s.DeviceId == deviceId)
                    .ToListAsync(cancellationToken);

                foreach (var supply in supplies)
                {
                    var forecast = await ForecastSupplyAsync(deviceId, supply.Kind, parameters, cancellationToken);
                    if (forecast != null)
                    {
                        results.Add(forecast);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating forecasts for device {DeviceId}", deviceId);
            }

            _logger.LogInformation("Generated {ForecastCount} forecasts for device {DeviceId}", 
                results.Count, deviceId);

            return results;
        }

        public async Task<ForecastResult?> ForecastSupplyAsync(
            int deviceId, 
            SupplyKind supplyKind, 
            ForecastParameters? parameters = null, 
            CancellationToken cancellationToken = default)
        {
            parameters ??= new ForecastParameters();
            
            _logger.LogDebug("Generating forecast for device {DeviceId}, supply {SupplyKind}", 
                deviceId, supplyKind);

            try
            {
                // Get historical data for this supply
                var historicalData = await _context.ForecastSnapshots
                    .Where(f => f.DeviceId == deviceId && f.SupplyKind == supplyKind)
                    .OrderBy(f => f.At)
                    .Take(parameters.MinimumDataPoints)
                    .ToListAsync(cancellationToken);

                if (historicalData.Count < parameters.MinimumDataPoints)
                {
                    _logger.LogDebug("Insufficient historical data for device {DeviceId}, supply {SupplyKind}", 
                        deviceId, supplyKind);
                    return null;
                }

                // Calculate daily usage using EWMA
                var dailyUsage = await CalculateDailyUsageAsync(deviceId, supplyKind, parameters, cancellationToken);
                
                if (!dailyUsage.HasValue || dailyUsage.Value <= 0)
                {
                    _logger.LogDebug("Unable to calculate daily usage for device {DeviceId}, supply {SupplyKind}", 
                        deviceId, supplyKind);
                    return null;
                }

                // Get current supply level
                var currentSupply = await _context.Supplies
                    .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.Kind == supplyKind, cancellationToken);

                if (currentSupply?.Percent == null)
                {
                    _logger.LogDebug("No current supply level for device {DeviceId}, supply {SupplyKind}", 
                        deviceId, supplyKind);
                    return null;
                }

                // Calculate days left
                var daysLeft = (int)Math.Floor(currentSupply.Percent.Value / dailyUsage.Value);

                // Calculate usage variance (simplified)
                var usageVariance = CalculateUsageVariance(historicalData, dailyUsage.Value);

                var result = new ForecastResult
                {
                    SupplyKind = supplyKind,
                    DaysLeft = daysLeft,
                    Confidence = Math.Min(1.0, 0.5 + (historicalData.Count / (double)parameters.MinimumDataPoints) * 0.5),
                    DailyUsage = dailyUsage.Value,
                    UsageVariance = usageVariance,
                    Model = "EWMA",
                    Parameters = new Dictionary<string, object>
                    {
                        ["alpha"] = parameters.EwmaAlpha,
                        ["data_points"] = historicalData.Count
                    }
                };

                // Save forecast snapshot
                var snapshot = new ForecastSnapshot
                {
                    DeviceId = deviceId,
                    SupplyKind = supplyKind,
                    DaysLeft = daysLeft,
                    Confidence = result.Confidence,
                    DailyUsage = dailyUsage.Value,
                    UsageVariance = usageVariance,
                    Model = "EWMA",
                    Parameters = System.Text.Json.JsonSerializer.Serialize(result.Parameters),
                    At = DateTime.UtcNow
                };

                _context.ForecastSnapshots.Add(snapshot);
                await _context.SaveChangesAsync(cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating forecast for device {DeviceId}, supply {SupplyKind}", 
                    deviceId, supplyKind);
                return null;
            }
        }

        public async Task<double?> CalculateDailyUsageAsync(
            int deviceId, 
            SupplyKind supplyKind, 
            ForecastParameters parameters, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Calculating daily usage for device {DeviceId}, supply {SupplyKind}", 
                deviceId, supplyKind);

            try
            {
                // Get recent supply level history
                var supplyHistory = await _context.Supplies
                    .Where(s => s.DeviceId == deviceId && s.Kind == supplyKind && s.Percent.HasValue)
                    .OrderByDescending(s => s.UpdatedAt)
                    .Take(10)
                    .ToListAsync(cancellationToken);

                if (supplyHistory.Count < 2)
                {
                    return EstimateDailyUsage(supplyKind);
                }

                // Calculate EWMA of daily usage
                var usageRates = new List<double>();
                for (int i = 1; i < supplyHistory.Count; i++)
                {
                    var timeDiff = supplyHistory[i - 1].UpdatedAt - supplyHistory[i].UpdatedAt;
                    if (timeDiff.TotalHours > 0)
                    {
                        var percentDiff = supplyHistory[i - 1].Percent.Value - supplyHistory[i].Percent.Value;
                        if (percentDiff > 0) // Only consider consumption, not refills
                        {
                            var dailyRate = percentDiff / (timeDiff.TotalHours / 24.0);
                            usageRates.Add(dailyRate);
                        }
                    }
                }

                if (usageRates.Count == 0)
                {
                    return EstimateDailyUsage(supplyKind);
                }

                // Apply EWMA
                var ewma = usageRates[0];
                for (int i = 1; i < usageRates.Count; i++)
                {
                    ewma = parameters.EwmaAlpha * usageRates[i] + (1 - parameters.EwmaAlpha) * ewma;
                }

                return Math.Max(0.01, ewma); // Ensure positive value
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily usage for device {DeviceId}, supply {SupplyKind}", 
                    deviceId, supplyKind);
                return EstimateDailyUsage(supplyKind);
            }
        }

        public async Task UpdateAllForecastsAsync(
            ForecastParameters? parameters = null, 
            CancellationToken cancellationToken = default)
        {
            parameters ??= new ForecastParameters();
            
            _logger.LogInformation("Starting forecast update for all devices");

            try
            {
                // Get all active devices with supplies
                var devices = await _context.Devices
                    .Where(d => d.IsActive)
                    .Include(d => d.Supplies)
                    .ToListAsync(cancellationToken);

                foreach (var device in devices)
                {
                    if (device.Supplies.Any())
                    {
                        await ForecastDeviceSuppliesAsync(device.Id, parameters, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating all forecasts");
            }

            _logger.LogInformation("Completed forecast update for all devices");
        }

        public (int? lowerBound, int? upperBound) GetConfidenceBand(
            ForecastResult forecast, 
            double confidenceLevel = 0.8)
        {
            if (!forecast.DaysLeft.HasValue || !forecast.UsageVariance.HasValue)
            {
                return (null, null);
            }

            var standardDeviation = Math.Sqrt(forecast.UsageVariance.Value);
            var margin = standardDeviation * GetConfidenceMultiplier(confidenceLevel);
            
            var lowerBound = (int)Math.Max(0, Math.Floor(forecast.DaysLeft.Value - margin));
            var upperBound = (int)Math.Ceiling(forecast.DaysLeft.Value + margin);

            return (lowerBound, upperBound);
        }

        private static double GetConfidenceMultiplier(double confidenceLevel)
        {
            // Z-scores for common confidence levels
            return confidenceLevel switch
            {
                >= 0.99 => 2.576,
                >= 0.95 => 1.96,
                >= 0.90 => 1.645,
                >= 0.80 => 1.282,
                _ => 1.0
            };
        }

        private static double EstimateDailyUsage(SupplyKind supplyKind)
        {
            // Simplified usage estimation based on supply type
            return supplyKind switch
            {
                SupplyKind.Black => 2.5,      // 2.5% per day for black toner
                SupplyKind.Cyan => 1.0,       // 1% per day for color toners
                SupplyKind.Magenta => 1.0,
                SupplyKind.Yellow => 1.0,
                SupplyKind.Drum => 0.5,       // 0.5% per day for drum
                SupplyKind.Fuser => 0.3,      // 0.3% per day for fuser
                _ => 1.0                      // Default 1% per day
            };
        }

        private static double CalculateUsageVariance(List<ForecastSnapshot> historicalData, double mean)
        {
            if (historicalData.Count < 2)
                return 0.0;

            var sum = historicalData.Where(f => f.DailyUsage.HasValue)
                .Sum(f => Math.Pow(f.DailyUsage!.Value - mean, 2));
            
            return sum / (historicalData.Count - 1);
        }
    }
}