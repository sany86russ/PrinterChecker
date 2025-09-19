using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Models;

namespace TonerWatch.Protocols.SNMP;

/// <summary>
/// Connection manager for efficient SNMP connection handling
/// </summary>
public class SnmpConnectionManager
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, SnmpConnection> _connections;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConnections;
    private readonly int _connectionTimeoutMs;

    public SnmpConnectionManager(ILogger logger, int maxConnections = 50, int connectionTimeoutMs = 5000)
    {
        _logger = logger;
        _connections = new Dictionary<string, SnmpConnection>();
        _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
        _maxConnections = maxConnections;
        _connectionTimeoutMs = connectionTimeoutMs;
    }

    /// <summary>
    /// Acquire a connection for the specified IP address and credentials
    /// </summary>
    public async Task<SnmpConnection> AcquireConnectionAsync(IPAddress ipAddress, Credential? credential, CancellationToken cancellationToken = default)
    {
        var key = GenerateConnectionKey(ipAddress, credential);
        
        // Wait for available connection slot
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Check if we already have a connection for this key
            if (_connections.TryGetValue(key, out var existingConnection) && !existingConnection.IsExpired)
            {
                existingConnection.LastUsed = DateTime.UtcNow;
                return existingConnection;
            }
            
            // Create new connection
            var connection = new SnmpConnection(ipAddress, credential, _connectionTimeoutMs);
            _connections[key] = connection;
            
            _logger.LogDebug("Acquired SNMP connection for {IpAddress}", ipAddress);
            return connection;
        }
        catch
        {
            // Release semaphore if connection creation fails
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Release a connection back to the pool
    /// </summary>
    public void ReleaseConnection(SnmpConnection connection)
    {
        // Simply release the semaphore - connections are kept for reuse
        _semaphore.Release();
        _logger.LogDebug("Released SNMP connection for {IpAddress}", connection.IpAddress);
    }

    /// <summary>
    /// Clean up expired connections
    /// </summary>
    public void CleanupExpiredConnections()
    {
        var expiredKeys = _connections
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_connections.Remove(key, out var connection))
            {
                connection.Dispose();
                _logger.LogDebug("Cleaned up expired connection for {IpAddress}", connection.IpAddress);
            }
        }
    }

    /// <summary>
    /// Close all connections
    /// </summary>
    public void CloseAllConnections()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
        _logger.LogInformation("Closed all SNMP connections");
    }

    private string GenerateConnectionKey(IPAddress ipAddress, Credential? credential)
    {
        if (credential == null)
        {
            return $"{ipAddress}:public";
        }

        return credential.Type switch
        {
            CredentialType.SNMPv1 => $"{ipAddress}:{credential.Type}:public",
            CredentialType.SNMPv2c => $"{ipAddress}:{credential.Type}:public",
            CredentialType.SNMPv3 => $"{ipAddress}:{credential.Type}:public", // Simplified - in real implementation would include more details
            _ => $"{ipAddress}:{credential.Type}:public"
        };
    }

    public void Dispose()
    {
        CloseAllConnections();
        _semaphore?.Dispose();
    }
}

/// <summary>
/// Represents an SNMP connection
/// </summary>
public class SnmpConnection : IDisposable
{
    public IPAddress IpAddress { get; }
    public Credential? Credential { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastUsed { get; set; }
    public int TimeoutMs { get; }
    
    // Connection will expire after 5 minutes of inactivity
    public bool IsExpired => DateTime.UtcNow.Subtract(LastUsed).TotalMinutes > 5;

    public SnmpConnection(IPAddress ipAddress, Credential? credential, int timeoutMs)
    {
        IpAddress = ipAddress;
        Credential = credential;
        TimeoutMs = timeoutMs;
        CreatedAt = DateTime.UtcNow;
        LastUsed = DateTime.UtcNow;
    }

    public void Dispose()
    {
        // Cleanup any resources if needed
    }
}