namespace TonerWatch.Core.Models;

/// <summary>
/// Credential types for device authentication
/// </summary>
public enum CredentialType
{
    SNMPv1 = 0,
    SNMPv2c = 1,
    SNMPv3 = 2,
    IPP = 3,
    HTTP = 4,
    WindowsAuth = 5,
    Certificate = 6
}

/// <summary>
/// Credential scope
/// </summary>
public enum CredentialScope
{
    Global = 0,
    Site = 1,
    Device = 2
}

/// <summary>
/// Credential entity for storing authentication information
/// </summary>
public class Credential
{
    public int Id { get; set; }
    public CredentialScope Scope { get; set; }
    public int? SiteId { get; set; }
    public int? DeviceId { get; set; }
    public CredentialType Type { get; set; }
    public required string Name { get; set; }
    public required string SecretRef { get; set; } // Reference to DPAPI encrypted secret
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0; // Higher priority credentials are tried first
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsed { get; set; }

    // Navigation properties
    public virtual Site? Site { get; set; }
    public virtual Device? Device { get; set; }
}

/// <summary>
/// Secret data structure for credentials (stored encrypted)
/// </summary>
public class CredentialSecret
{
    // SNMPv2c
    public string? Community { get; set; }

    // SNMPv3
    public string? Username { get; set; }
    public string? AuthProtocol { get; set; } // MD5, SHA, SHA224, SHA256, SHA384, SHA512
    public string? AuthPassword { get; set; }
    public string? PrivProtocol { get; set; } // DES, AES128, AES192, AES256
    public string? PrivPassword { get; set; }
    public string? ContextName { get; set; }

    // HTTP/IPP
    public string? HttpUsername { get; set; }
    public string? HttpPassword { get; set; }
    public string? ApiKey { get; set; }
    public string? BearerToken { get; set; }

    // Certificate
    public string? CertificateThumbprint { get; set; }
    public string? CertificatePassword { get; set; }
    public byte[]? CertificateData { get; set; }
}