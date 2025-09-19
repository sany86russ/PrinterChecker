using Microsoft.Extensions.Logging;
using System.Net;
using TonerWatch.Core.Extensions;
using TonerWatch.Core.Models;

namespace TonerWatch.Discovery;

public class NetworkSegmentManager
{
    private readonly ILogger<NetworkSegmentManager> _logger;
    private List<NetworkSegment> _segments = new();

    public NetworkSegmentManager(ILogger<NetworkSegmentManager> logger)
    {
        _logger = logger;
    }

    public void AddSegment(NetworkSegment segment)
    {
        _segments.Add(segment);
        _logger.LogInformation("Added network segment: {Name} ({IpRange})", segment.Name, segment.IpRange);
    }

    public void RemoveSegment(string name)
    {
        _segments.RemoveAll(s => s.Name == name);
        _logger.LogInformation("Removed network segment: {Name}", name);
    }

    public List<NetworkSegment> GetAllSegments()
    {
        return _segments;
    }

    public async Task<List<IPAddress>> GetAllIpAddressesAsync()
    {
        var allIps = new List<IPAddress>();
        
        foreach (var segment in _segments)
        {
            var ips = IpRangeParser.ParseMultipleRanges(segment.IpRange);
            allIps.AddRange(ips);
        }
        
        return allIps;
    }

    public async Task<List<IPAddress>> GetIpAddressesForSegmentAsync(string segmentName)
    {
        var segment = _segments.FirstOrDefault(s => s.Name == segmentName);
        if (segment == null)
        {
            return new List<IPAddress>();
        }
        
        return IpRangeParser.ParseMultipleRanges(segment.IpRange).ToList();
    }
}