using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Infrastructure.Services;

public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;
    
    // In-memory storage for demonstration - in real implementation this would use repository
    private readonly Dictionary<string, Alert> _activeAlerts = new();
    private readonly List<AlertRule> _alertRules = new();

    public AlertService(ILogger<AlertService> logger)
    {
        _logger = logger;
        InitializeDefaultRules();
    }

    public async Task<IEnumerable<Alert>> EvaluateDeviceAlertsAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Evaluating alerts for device {DeviceId}", deviceId);

        var alerts = new List<Alert>();
        
        // Get applicable rules for this device
        var rules = await GetAlertRulesAsync(deviceId: deviceId, cancellationToken: cancellationToken);
        
        // In real implementation, this would fetch current device state and supplies
        // For now, simulate some alert conditions
        
        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            // Simulate alert evaluation based on rule type
            var alert = await EvaluateRuleForDevice(rule, deviceId);
            if (alert != null)
            {
                alerts.Add(alert);
            }
        }

        _logger.LogInformation("Generated {AlertCount} alerts for device {DeviceId}", alerts.Count, deviceId);
        return alerts;
    }

    public async Task<Alert?> EvaluateSupplyAlertAsync(AlertContext context, CancellationToken cancellationToken = default)
    {
        if (context.Supply == null || !context.CurrentValue.HasValue)
        {
            return null;
        }

        _logger.LogDebug("Evaluating supply alert for device {DeviceId}, supply {SupplyKind}, level {Level}%", 
            context.Device.Id, context.Supply.Kind, context.CurrentValue);

        // Get prioritized rules for this device
        var rules = await GetPrioritizedAlertRulesAsync(context.Device.Id, cancellationToken);
        var supplyRules = rules.Where(r => r.IsEnabled && 
            (r.SupplyKind == null || r.SupplyKind == context.Supply.Kind));

        foreach (var rule in supplyRules)
        {
            var alert = await EvaluateSupplyRule(rule, context);
            if (alert != null)
            {
                return await ProcessAlertWithDeduplication(alert);
            }
        }

        return null;
    }

    public async Task<IEnumerable<Alert>> ProcessAlertBatchAsync(IEnumerable<AlertContext> contexts, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing batch of {ContextCount} alert contexts", contexts.Count());

        var alerts = new List<Alert>();
        
        foreach (var context in contexts)
        {
            try
            {
                var alert = await EvaluateSupplyAlertAsync(context, cancellationToken);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate alert for device {DeviceId}", context.Device.Id);
            }
        }

        return alerts;
    }

    public async Task<bool> AcknowledgeAlertAsync(int alertId, string acknowledgedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Acknowledging alert {AlertId} by {User}", alertId, acknowledgedBy);

        // In real implementation, this would update the database
        var alert = _activeAlerts.Values.FirstOrDefault(a => a.Id == alertId);
        if (alert != null && alert.Status == AlertStatus.Active)
        {
            alert.Status = AlertStatus.Acknowledged;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = acknowledgedBy;
            return true;
        }

        return false;
    }

    public async Task<bool> ResolveAlertAsync(int alertId, string resolvedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resolving alert {AlertId} by {User}", alertId, resolvedBy);

        var alert = _activeAlerts.Values.FirstOrDefault(a => a.Id == alertId);
        if (alert != null && alert.Status != AlertStatus.Resolved)
        {
            alert.Status = AlertStatus.Resolved;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = resolvedBy;
            
            // Remove from active alerts
            _activeAlerts.Remove(alert.AlertKey);
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsAsync(int? siteId = null, int? deviceId = null, AlertSeverity? minSeverity = null, CancellationToken cancellationToken = default)
    {
        var alerts = _activeAlerts.Values.Where(a => a.Status == AlertStatus.Active);

        if (deviceId.HasValue)
        {
            alerts = alerts.Where(a => a.DeviceId == deviceId.Value);
        }

        if (siteId.HasValue)
        {
            alerts = alerts.Where(a => a.Device.SiteId == siteId.Value);
        }

        if (minSeverity.HasValue)
        {
            alerts = alerts.Where(a => a.Severity >= minSeverity.Value);
        }

        return alerts.OrderByDescending(a => a.Severity)
                    .ThenByDescending(a => a.LastOccurrence);
    }

    public async Task<IEnumerable<AlertRule>> GetAlertRulesAsync(int? siteId = null, int? deviceId = null, CancellationToken cancellationToken = default)
    {
        var rules = _alertRules.AsEnumerable();

        if (siteId.HasValue)
        {
            rules = rules.Where(r => r.SiteId == null || r.SiteId == siteId.Value);
        }

        if (deviceId.HasValue)
        {
            // Include both general rules and device-specific rules
            rules = rules.Where(r => r.DeviceId == null || r.DeviceId == deviceId.Value);
        }

        return rules;
    }

    /// <summary>
    /// Get alert rules prioritized by specificity (device-specific first)
    /// </summary>
    public async Task<IEnumerable<AlertRule>> GetPrioritizedAlertRulesAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        var rules = _alertRules.Where(r => r.IsEnabled);
        
        // Filter for device-specific rules first
        var deviceSpecificRules = rules.Where(r => r.DeviceId == deviceId);
        var generalRules = rules.Where(r => r.DeviceId == null);
        
        // Return device-specific rules first, then general rules
        return deviceSpecificRules.Concat(generalRules);
    }

    public async Task<AlertRule> SaveAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving alert rule {RuleName}", rule.Name);

        if (rule.Id == 0)
        {
            rule.Id = _alertRules.Count + 1;
            rule.CreatedAt = DateTime.UtcNow;
            _alertRules.Add(rule);
        }
        else
        {
            var existingRule = _alertRules.FirstOrDefault(r => r.Id == rule.Id);
            if (existingRule != null)
            {
                var index = _alertRules.IndexOf(existingRule);
                rule.UpdatedAt = DateTime.UtcNow;
                _alertRules[index] = rule;
            }
        }

        return rule;
    }

    /// <summary>
    /// Create or update device-specific alert rule
    /// </summary>
    public async Task<AlertRule> SaveDeviceSpecificRuleAsync(int deviceId, SupplyKind supplyKind, double warningThreshold, double criticalThreshold, CancellationToken cancellationToken = default)
    {
        // Check if a device-specific rule already exists
        var existingRule = _alertRules.FirstOrDefault(r => r.DeviceId == deviceId && r.SupplyKind == supplyKind);
        
        if (existingRule == null)
        {
            existingRule = new AlertRule
            {
                Name = $"Device {deviceId} - {supplyKind} Thresholds",
                Category = AlertCategory.SupplyLow,
                DefaultSeverity = AlertSeverity.Warning,
                DeviceId = deviceId,
                SupplyKind = supplyKind,
                WarningThreshold = warningThreshold,
                CriticalThreshold = criticalThreshold,
                HysteresisMargin = 2.0,
                DeduplicationWindow = TimeSpan.FromMinutes(30),
                IsEnabled = true
            };
            
            return await SaveAlertRuleAsync(existingRule, cancellationToken);
        }
        else
        {
            existingRule.WarningThreshold = warningThreshold;
            existingRule.CriticalThreshold = criticalThreshold;
            existingRule.UpdatedAt = DateTime.UtcNow;
            return await SaveAlertRuleAsync(existingRule, cancellationToken);
        }
    }

    public async Task<bool> DeleteAlertRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting alert rule {RuleId}", ruleId);

        var rule = _alertRules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            _alertRules.Remove(rule);
            return true;
        }

        return false;
    }

    private async Task<Alert?> EvaluateRuleForDevice(AlertRule rule, int deviceId)
    {
        // Simulate device-level alerts
        switch (rule.Category)
        {
            case AlertCategory.DeviceOffline:
                // Simulate random offline detection
                if (Random.Shared.NextDouble() < 0.05) // 5% chance
                {
                    return CreateDeviceAlert(rule, deviceId, "Device Offline", "Device is not responding to SNMP requests");
                }
                break;

            case AlertCategory.DeviceError:
                // Simulate error conditions
                if (Random.Shared.NextDouble() < 0.02) // 2% chance
                {
                    return CreateDeviceAlert(rule, deviceId, "Device Error", "Device reported error status");
                }
                break;
        }

        return null;
    }

    private async Task<Alert?> EvaluateSupplyRule(AlertRule rule, AlertContext context)
    {
        if (!context.CurrentValue.HasValue || context.Supply == null)
        {
            return null;
        }

        var currentLevel = context.CurrentValue.Value;
        var supply = context.Supply;

        // Apply hysteresis to prevent flapping
        var warningThreshold = rule.WarningThreshold ?? 25.0;
        var criticalThreshold = rule.CriticalThreshold ?? 10.0;
        var hysteresis = rule.HysteresisMargin ?? 2.0;

        // Check if we're transitioning from a higher level (apply hysteresis)
        if (context.PreviousValue.HasValue && context.PreviousValue.Value > currentLevel)
        {
            warningThreshold += hysteresis;
            criticalThreshold += hysteresis;
        }

        AlertSeverity? severity = null;
        string title = "";
        string description = "";

        if (currentLevel <= criticalThreshold)
        {
            severity = AlertSeverity.Critical;
            title = $"Critical Supply Level - {supply.Kind}";
            description = $"{supply.Kind} supply is critically low at {currentLevel:F1}% (threshold: {criticalThreshold}%)";
        }
        else if (currentLevel <= warningThreshold)
        {
            severity = AlertSeverity.Warning;
            title = $"Low Supply Level - {supply.Kind}";
            description = $"{supply.Kind} supply is low at {currentLevel:F1}% (threshold: {warningThreshold}%)";
        }

        if (severity.HasValue)
        {
            var alertKey = GenerateAlertKey(context.Device.Id, supply.Kind, rule.Category);
            
            return new Alert
            {
                AlertKey = alertKey,
                Rule = rule,
                RuleId = rule.Id,
                Device = context.Device,
                DeviceId = context.Device.Id,
                Severity = severity.Value,
                Category = rule.Category,
                Title = title,
                Description = description,
                SupplyKind = supply.Kind,
                CurrentLevel = currentLevel,
                ThresholdValue = severity == AlertSeverity.Critical ? criticalThreshold : warningThreshold,
                Metadata = new Dictionary<string, object>
                {
                    ["supply_id"] = supply.Id,
                    ["vendor"] = context.Device.Vendor,
                    ["model"] = context.Device.Model,
                    ["location"] = context.Device.Location ?? "Unknown"
                }
            };
        }

        return null;
    }

    private async Task<Alert> ProcessAlertWithDeduplication(Alert newAlert)
    {
        var existingAlert = _activeAlerts.GetValueOrDefault(newAlert.AlertKey);
        
        if (existingAlert != null)
        {
            // Check deduplication window
            var timeSinceLastOccurrence = DateTime.UtcNow - existingAlert.LastOccurrence;
            
            if (timeSinceLastOccurrence < newAlert.Rule.DeduplicationWindow)
            {
                // Within deduplication window - update existing alert
                existingAlert.LastOccurrence = DateTime.UtcNow;
                existingAlert.OccurrenceCount++;
                existingAlert.CurrentLevel = newAlert.CurrentLevel;
                existingAlert.Severity = newAlert.Severity; // Allow severity escalation
                
                _logger.LogDebug("Deduplicated alert {AlertKey}, occurrence count: {Count}", 
                    newAlert.AlertKey, existingAlert.OccurrenceCount);
                
                return existingAlert;
            }
            else
            {
                // Outside deduplication window - create new occurrence
                _activeAlerts.Remove(newAlert.AlertKey);
            }
        }

        // Create new alert
        newAlert.Id = _activeAlerts.Count + 1;
        _activeAlerts[newAlert.AlertKey] = newAlert;
        
        _logger.LogInformation("Created new alert {AlertKey} for device {DeviceId}", 
            newAlert.AlertKey, newAlert.DeviceId);
        
        return newAlert;
    }

    private Alert CreateDeviceAlert(AlertRule rule, int deviceId, string title, string description)
    {
        var alertKey = GenerateAlertKey(deviceId, null, rule.Category);
        
        return new Alert
        {
            AlertKey = alertKey,
            Rule = rule,
            RuleId = rule.Id,
            DeviceId = deviceId,
            Severity = rule.DefaultSeverity,
            Category = rule.Category,
            Title = title,
            Description = description
        };
    }

    private string GenerateAlertKey(int deviceId, SupplyKind? supplyKind, AlertCategory category)
    {
        var key = $"{deviceId}_{category}";
        if (supplyKind.HasValue)
        {
            key += $"_{supplyKind}";
        }
        return key;
    }

    private void InitializeDefaultRules()
    {
        // Default supply level rules
        _alertRules.AddRange(new[]
        {
            new AlertRule
            {
                Id = 1,
                Name = "Low Toner Warning",
                Category = AlertCategory.SupplyLow,
                DefaultSeverity = AlertSeverity.Warning,
                WarningThreshold = 25.0,
                CriticalThreshold = 10.0,
                HysteresisMargin = 3.0,
                DeduplicationWindow = TimeSpan.FromMinutes(30)
            },
            new AlertRule
            {
                Id = 2,
                Name = "Critical Toner Level",
                Category = AlertCategory.SupplyCritical,
                DefaultSeverity = AlertSeverity.Critical,
                CriticalThreshold = 5.0,
                HysteresisMargin = 2.0,
                DeduplicationWindow = TimeSpan.FromMinutes(15)
            },
            new AlertRule
            {
                Id = 3,
                Name = "Device Offline",
                Category = AlertCategory.DeviceOffline,
                DefaultSeverity = AlertSeverity.Critical,
                DeduplicationWindow = TimeSpan.FromMinutes(10)
            },
            new AlertRule
            {
                Id = 4,
                Name = "Device Error",
                Category = AlertCategory.DeviceError,
                DefaultSeverity = AlertSeverity.Warning,
                DeduplicationWindow = TimeSpan.FromMinutes(20)
            }
        });

        _logger.LogInformation("Initialized {RuleCount} default alert rules", _alertRules.Count);
    }
}