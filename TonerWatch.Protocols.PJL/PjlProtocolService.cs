using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;

namespace TonerWatch.Protocols.PJL;

/// <summary>
/// PJL (Printer Job Language) protocol implementation
/// </summary>
public class PjlProtocolService : IPjlProtocol
{
    private readonly ILogger<PjlProtocolService> _logger;
    private const int DefaultPort = 9100;

    public string Name => "PJL";

    public PjlProtocolService(ILogger<PjlProtocolService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, DefaultPort);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        return await IsAvailableAsync(ipAddress, cancellationToken);
    }

    public async Task<DeviceInfo?> GetDeviceInfoAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        // Basic implementation - would need to send PJL commands in a full implementation
        return new DeviceInfo
        {
            Status = DeviceStatus.Online,
            Capabilities = DeviceCapabilities.PJL
        };
    }

    public async Task<IEnumerable<SupplyReading>> GetSupplyLevelsAsync(IPAddress ipAddress, Credential? credential = null, CancellationToken cancellationToken = default)
    {
        // PJL supply information would come from INFO commands
        return new List<SupplyReading>();
    }

    public async Task<Dictionary<string, string>> SendCommandAsync(IPAddress ipAddress, string command, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string>();

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, DefaultPort);
            
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream);
            using var reader = new StreamReader(stream);

            // Send PJL command
            await writer.WriteLineAsync("@PJL " + command);
            await writer.WriteLineAsync("@PJL EOJ");
            await writer.FlushAsync();

            // Read response
            var response = await reader.ReadToEndAsync();
            results["response"] = response;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send PJL command to {IpAddress}", ipAddress);
            results["error"] = ex.Message;
        }

        return results;
    }

    public async Task<string?> GetInfoAsync(IPAddress ipAddress, string category, CancellationToken cancellationToken = default)
    {
        try
        {
            var command = $"INFO {category}";
            var results = await SendCommandAsync(ipAddress, command, cancellationToken);
            return results.GetValueOrDefault("response");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get PJL info from {IpAddress}", ipAddress);
            return null;
        }
    }
}