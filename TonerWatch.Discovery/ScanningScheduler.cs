using Microsoft.Extensions.Logging;
using System.Timers;
using TonerWatch.Core.Models;

namespace TonerWatch.Discovery;

/// <summary>
/// Scheduler for network scanning operations to avoid peak usage times
/// </summary>
public class ScanningScheduler
{
    private readonly ILogger<ScanningScheduler> _logger;
    private readonly System.Timers.Timer _scanTimer;
    private readonly NetworkSegmentManager _segmentManager;
    private List<NetworkSegment> _segments = new();
    private bool _avoidPeakTimes = false;
    private string _globalSchedule = "0 0 * * *"; // Default: every hour
    private bool _isEnabled = false;

    public event EventHandler<ScanTriggeredEventArgs>? ScanTriggered;

    public ScanningScheduler(ILogger<ScanningScheduler> logger, NetworkSegmentManager segmentManager)
    {
        _logger = logger;
        _segmentManager = segmentManager;
        
        // Set up timer for periodic scanning
        _scanTimer = new System.Timers.Timer
        {
            AutoReset = true
        };
        _scanTimer.Elapsed += OnScanTimerElapsed;
    }

    /// <summary>
    /// Configure the scheduler with settings
    /// </summary>
    public void Configure(List<NetworkSegment> segments, bool avoidPeakTimes, string globalSchedule, bool isEnabled)
    {
        _segments = segments;
        _avoidPeakTimes = avoidPeakTimes;
        _globalSchedule = globalSchedule;
        _isEnabled = isEnabled;

        // Update timer interval based on schedule
        UpdateTimerInterval();
    }

    /// <summary>
    /// Start the scheduler
    /// </summary>
    public void Start()
    {
        if (_isEnabled)
        {
            _scanTimer.Start();
            _logger.LogInformation("Scanning scheduler started");
        }
    }

    /// <summary>
    /// Stop the scheduler
    /// </summary>
    public void Stop()
    {
        _scanTimer.Stop();
        _logger.LogInformation("Scanning scheduler stopped");
    }

    /// <summary>
    /// Update timer interval based on schedule
    /// </summary>
    private void UpdateTimerInterval()
    {
        // Simple implementation - in a real system, you'd parse cron expressions
        // For now, we'll use a simple approach based on the global schedule
        
        var interval = TimeSpan.FromHours(1); // Default to hourly
        
        if (_globalSchedule.Contains("every"))
        {
            var parts = _globalSchedule.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minutes))
            {
                interval = TimeSpan.FromMinutes(minutes);
            }
        }
        
        _scanTimer.Interval = interval.TotalMilliseconds;
    }

    /// <summary>
    /// Timer elapsed handler
    /// </summary>
    private void OnScanTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Check if we should avoid scanning during peak times
        if (_avoidPeakTimes && IsPeakTime())
        {
            _logger.LogInformation("Skipping scan during peak hours");
            return;
        }

        // Trigger scan
        OnScanTriggered();
    }

    /// <summary>
    /// Check if current time is during peak hours (9AM-5PM)
    /// </summary>
    private bool IsPeakTime()
    {
        var hour = DateTime.Now.Hour;
        return hour >= 9 && hour < 17; // 9AM to 5PM
    }

    /// <summary>
    /// Trigger a scan
    /// </summary>
    private void OnScanTriggered()
    {
        _logger.LogInformation("Scheduler triggering network scan");
        ScanTriggered?.Invoke(this, new ScanTriggeredEventArgs(_segments));
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _scanTimer.Stop();
        _scanTimer.Dispose();
    }
}

/// <summary>
/// Event arguments for scan triggered events
/// </summary>
public class ScanTriggeredEventArgs : EventArgs
{
    public List<NetworkSegment> Segments { get; }

    public ScanTriggeredEventArgs(List<NetworkSegment> segments)
    {
        Segments = segments;
    }
}