namespace TonerWatch.Core.Extensions;

/// <summary>
/// DateTime utility extensions
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Convert UTC time to site timezone
    /// </summary>
    public static DateTime ToSiteTime(this DateTime utcDateTime, string timezoneId)
    {
        if (string.IsNullOrEmpty(timezoneId))
            return utcDateTime.ToLocalTime();

        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timezone);
        }
        catch
        {
            return utcDateTime.ToLocalTime();
        }
    }

    /// <summary>
    /// Convert site time to UTC
    /// </summary>
    public static DateTime ToUtcFromSite(this DateTime siteDateTime, string timezoneId)
    {
        if (string.IsNullOrEmpty(timezoneId))
            return siteDateTime.ToUniversalTime();

        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTimeToUtc(siteDateTime, timezone);
        }
        catch
        {
            return siteDateTime.ToUniversalTime();
        }
    }

    /// <summary>
    /// Check if current time is within quiet hours
    /// </summary>
    public static bool IsInQuietHours(this DateTime dateTime, TimeSpan startTime, TimeSpan endTime, string timezoneId)
    {
        var siteTime = dateTime.ToSiteTime(timezoneId);
        var currentTime = siteTime.TimeOfDay;

        if (startTime <= endTime)
        {
            // Same day range (e.g., 9 AM to 5 PM)
            return currentTime >= startTime && currentTime <= endTime;
        }
        else
        {
            // Overnight range (e.g., 6 PM to 8 AM)
            return currentTime >= startTime || currentTime <= endTime;
        }
    }

    /// <summary>
    /// Get the start of the week (Monday)
    /// </summary>
    public static DateTime StartOfWeek(this DateTime dateTime)
    {
        var daysToSubtract = ((int)dateTime.DayOfWeek - 1 + 7) % 7;
        return dateTime.Date.AddDays(-daysToSubtract);
    }

    /// <summary>
    /// Get the start of the month
    /// </summary>
    public static DateTime StartOfMonth(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, 1);
    }

    /// <summary>
    /// Get the start of the day
    /// </summary>
    public static DateTime StartOfDay(this DateTime dateTime)
    {
        return dateTime.Date;
    }

    /// <summary>
    /// Get friendly relative time string
    /// </summary>
    public static string ToRelativeString(this DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow.Subtract(dateTime);

        return timeSpan switch
        {
            { TotalSeconds: <= 60 } => "just now",
            { TotalMinutes: <= 1 } => "about a minute ago",
            { TotalMinutes: < 60 } => $"about {(int)timeSpan.TotalMinutes} minutes ago",
            { TotalHours: <= 1 } => "about an hour ago",
            { TotalHours: < 24 } => $"about {(int)timeSpan.TotalHours} hours ago",
            { TotalDays: <= 1 } => "yesterday",
            { TotalDays: < 30 } => $"about {(int)timeSpan.TotalDays} days ago",
            { TotalDays: < 365 } => $"about {(int)(timeSpan.TotalDays / 30)} months ago",
            _ => $"about {(int)(timeSpan.TotalDays / 365)} years ago"
        };
    }

    /// <summary>
    /// Check if date is within business hours (Monday-Friday, 9 AM - 5 PM)
    /// </summary>
    public static bool IsBusinessHours(this DateTime dateTime, string timezoneId)
    {
        var siteTime = dateTime.ToSiteTime(timezoneId);
        
        return siteTime.DayOfWeek switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => false,
            _ => siteTime.TimeOfDay >= TimeSpan.FromHours(9) && siteTime.TimeOfDay <= TimeSpan.FromHours(17)
        };
    }
}