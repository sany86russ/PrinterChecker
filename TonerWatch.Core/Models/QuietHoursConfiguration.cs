using System.Text.Json;

namespace TonerWatch.Core.Models;

/// <summary>
/// Quiet hours configuration for notifications
/// </summary>
public class QuietHoursConfiguration
{
    public List<QuietHoursRange> Ranges { get; set; } = new();
    
    /// <summary>
    /// Check if notifications should be suppressed at the current time
    /// </summary>
    public bool IsQuietHours(DateTime? currentTime = null)
    {
        var now = currentTime ?? DateTime.Now;
        var timeOfDay = now.TimeOfDay;
        
        foreach (var range in Ranges)
        {
            if (range.IsWithinRange(timeOfDay))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Get the next time when notifications will be allowed
    /// </summary>
    public DateTime? GetNextAllowedTime(DateTime? currentTime = null)
    {
        var now = currentTime ?? DateTime.Now;
        
        // Find the next start time of a quiet hours range
        DateTime? nextAllowed = null;
        
        foreach (var range in Ranges)
        {
            var nextStart = range.GetNextStartTime(now);
            if (nextAllowed == null || nextStart < nextAllowed)
            {
                nextAllowed = nextStart;
            }
        }
        
        return nextAllowed;
    }
    
    /// <summary>
    /// Serialize to JSON string
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }
    
    /// <summary>
    /// Deserialize from JSON string
    /// </summary>
    public static QuietHoursConfiguration FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new QuietHoursConfiguration();
            
        try
        {
            return JsonSerializer.Deserialize<QuietHoursConfiguration>(json) ?? new QuietHoursConfiguration();
        }
        catch
        {
            return new QuietHoursConfiguration();
        }
    }
}

/// <summary>
/// Time range for quiet hours
/// </summary>
public class QuietHoursRange
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public List<DayOfWeek> Days { get; set; } = new();
    
    /// <summary>
    /// Check if the specified time is within this quiet hours range
    /// </summary>
    public bool IsWithinRange(TimeSpan timeOfDay)
    {
        // Check if today is an active day
        var today = DateTime.Now.DayOfWeek;
        if (Days.Any() && !Days.Contains(today))
        {
            return false;
        }
        
        // Check time range
        if (Start <= End)
        {
            // Same day range (e.g., 22:00-06:00)
            return timeOfDay >= Start && timeOfDay <= End;
        }
        else
        {
            // Overnight range (e.g., 22:00-06:00 crosses midnight)
            return timeOfDay >= Start || timeOfDay <= End;
        }
    }
    
    /// <summary>
    /// Get the next start time for this quiet hours range
    /// </summary>
    public DateTime GetNextStartTime(DateTime fromTime)
    {
        var today = fromTime.Date;
        var startTimeToday = today.Add(Start);
        
        if (fromTime < startTimeToday)
        {
            // Start time is today
            return startTimeToday;
        }
        else
        {
            // Start time is tomorrow
            return startTimeToday.AddDays(1);
        }
    }
}