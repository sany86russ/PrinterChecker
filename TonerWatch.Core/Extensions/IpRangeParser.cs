using System.Net;

namespace TonerWatch.Core.Extensions;

/// <summary>
/// IP range parsing utilities for handling various IP range formats
/// </summary>
public static class IpRangeParser
{
    /// <summary>
    /// Parse IP range in various formats: CIDR, range (192.168.1.1-254), or single IP
    /// </summary>
    public static IEnumerable<IPAddress> ParseIpRange(string range)
    {
        if (string.IsNullOrWhiteSpace(range))
            throw new ArgumentException("Range cannot be null or empty", nameof(range));

        range = range.Trim();

        // Handle range format: 192.168.1.1-254
        if (range.Contains('-') && !range.Contains('/'))
        {
            return ParseRangeFormat(range);
        }
        // Handle CIDR format: 192.168.1.0/24
        else if (range.Contains('/'))
        {
            return ParseCidrFormat(range);
        }
        // Handle single IP
        else
        {
            if (IPAddress.TryParse(range, out var ip))
            {
                return new[] { ip };
            }
            throw new ArgumentException($"Invalid IP format: {range}", nameof(range));
        }
    }

    /// <summary>
    /// Parse range format like "192.168.1.1-254" or "192.168.1.10-192.168.1.50"
    /// </summary>
    private static IEnumerable<IPAddress> ParseRangeFormat(string range)
    {
        var parts = range.Split('-');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid range format: {range}");

        // Handle format like "192.168.1.1-254"
        if (!parts[1].Contains('.'))
        {
            var baseIpParts = parts[0].Split('.');
            if (baseIpParts.Length != 4)
                throw new ArgumentException($"Invalid IP format: {parts[0]}");

            if (!int.TryParse(baseIpParts[3], out var startOctet) || 
                !int.TryParse(parts[1], out var endOctet))
                throw new ArgumentException($"Invalid range values: {range}");

            var baseIp = $"{baseIpParts[0]}.{baseIpParts[1]}.{baseIpParts[2]}.";
            
            for (int i = startOctet; i <= endOctet; i++)
            {
                if (IPAddress.TryParse($"{baseIp}{i}", out var ip))
                    yield return ip;
            }
        }
        // Handle format like "192.168.1.10-192.168.1.50"
        else
        {
            if (!IPAddress.TryParse(parts[0], out var startIp) ||
                !IPAddress.TryParse(parts[1], out var endIp))
                throw new ArgumentException($"Invalid IP range: {range}");

            var startBytes = startIp.GetAddressBytes();
            var endBytes = endIp.GetAddressBytes();

            // Only support IPv4 for now
            if (startBytes.Length != 4 || endBytes.Length != 4)
                throw new ArgumentException("Only IPv4 addresses are supported");

            // Convert to uint for easier comparison
            var startInt = BitConverter.ToUInt32(startBytes.Reverse().ToArray(), 0);
            var endInt = BitConverter.ToUInt32(endBytes.Reverse().ToArray(), 0);

            for (uint i = startInt; i <= endInt; i++)
            {
                var ipBytes = BitConverter.GetBytes(i).Reverse().ToArray();
                yield return new IPAddress(ipBytes);
            }
        }
    }

    /// <summary>
    /// Parse CIDR format like "192.168.1.0/24"
    /// </summary>
    private static IEnumerable<IPAddress> ParseCidrFormat(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid CIDR format: {cidr}");

        if (!IPAddress.TryParse(parts[0], out var baseAddress))
            throw new ArgumentException($"Invalid IP address: {parts[0]}");

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
            throw new ArgumentException($"Invalid prefix length: {parts[1]}");

        var mask = IPAddress.HostToNetworkOrder(-1 << (32 - prefixLength));
        var maskBytes = BitConverter.GetBytes(mask);
        var baseBytes = baseAddress.GetAddressBytes();
        
        if (baseBytes.Length != 4)
            throw new ArgumentException("Only IPv4 addresses are supported");

        var networkBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            networkBytes[i] = (byte)(baseBytes[i] & maskBytes[i]);
        }

        var hostCount = (1 << (32 - prefixLength)) - 2; // Exclude network and broadcast
        var networkInt = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);

        for (uint i = 1; i <= hostCount; i++)
        {
            var hostInt = networkInt + i;
            var hostBytes = BitConverter.GetBytes(hostInt).Reverse().ToArray();
            yield return new IPAddress(hostBytes);
        }
    }

    /// <summary>
    /// Parse multiple IP ranges separated by commas
    /// </summary>
    public static IEnumerable<IPAddress> ParseMultipleRanges(string ranges)
    {
        if (string.IsNullOrWhiteSpace(ranges))
            throw new ArgumentException("Ranges cannot be null or empty", nameof(ranges));

        var rangeList = ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allIps = new HashSet<IPAddress>();

        foreach (var range in rangeList)
        {
            foreach (var ip in ParseIpRange(range))
            {
                // Use HashSet to automatically deduplicate
                allIps.Add(ip);
            }
        }

        return allIps;
    }
}