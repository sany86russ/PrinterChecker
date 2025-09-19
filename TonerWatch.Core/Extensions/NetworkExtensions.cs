using System.Net;
using System.Net.NetworkInformation;

namespace TonerWatch.Core.Extensions;

/// <summary>
/// Network utility extensions
/// </summary>
public static class NetworkExtensions
{
    /// <summary>
    /// Parse CIDR subnet to get all IP addresses
    /// </summary>
    public static IEnumerable<IPAddress> GetSubnetAddresses(this string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid CIDR format", nameof(cidr));

        if (!IPAddress.TryParse(parts[0], out var baseAddress))
            throw new ArgumentException("Invalid IP address", nameof(cidr));

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
            throw new ArgumentException("Invalid prefix length", nameof(cidr));

        var mask = IPAddress.HostToNetworkOrder(-1 << (32 - prefixLength));
        var maskBytes = BitConverter.GetBytes(mask);
        var baseBytes = baseAddress.GetAddressBytes();
        
        if (baseBytes.Length != 4)
            throw new ArgumentException("Only IPv4 addresses are supported", nameof(cidr));

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
    /// Check if IP address is in subnet
    /// </summary>
    public static bool IsInSubnet(this IPAddress address, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;

        if (!IPAddress.TryParse(parts[0], out var baseAddress)) return false;
        if (!int.TryParse(parts[1], out var prefixLength)) return false;

        var mask = IPAddress.HostToNetworkOrder(-1 << (32 - prefixLength));
        var maskBytes = BitConverter.GetBytes(mask);
        var baseBytes = baseAddress.GetAddressBytes();
        var addrBytes = address.GetAddressBytes();

        if (baseBytes.Length != 4 || addrBytes.Length != 4) return false;

        for (int i = 0; i < 4; i++)
        {
            if ((baseBytes[i] & maskBytes[i]) != (addrBytes[i] & maskBytes[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Ping an IP address with timeout
    /// </summary>
    public static async Task<bool> PingAsync(this IPAddress address, TimeSpan timeout)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if TCP port is open
    /// </summary>
    public static async Task<bool> IsPortOpenAsync(this IPAddress address, int port, TimeSpan timeout)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(address, port);
            var timeoutTask = Task.Delay(timeout);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
                return false;
                
            await connectTask; // Will throw if connection failed
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get MAC address for IP (best effort using ARP)
    /// </summary>
    public static async Task<string?> GetMacAddressAsync(this IPAddress ipAddress)
    {
        try
        {
            // This is a simplified implementation - would need WinAPI calls for full functionality
            await Task.Delay(1); // Placeholder for async
            return null; // TODO: Implement ARP table lookup
        }
        catch
        {
            return null;
        }
    }
}