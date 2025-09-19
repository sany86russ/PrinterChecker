namespace TonerWatch.Core.Models;

/// <summary>
/// User roles for RBAC
/// </summary>
public enum UserRole
{
    Viewer = 1,
    Technician = 2,
    Admin = 3
}

/// <summary>
/// User entity for authentication and authorization
/// </summary>
public class User
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string? Username { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public string? ExternalId { get; set; } // For SSO integration
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public string? Preferences { get; set; } // JSON user preferences
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if user has permission for an action
    /// </summary>
    public bool HasPermission(string permission)
    {
        return permission switch
        {
            "view" => Role >= UserRole.Viewer,
            "manage" => Role >= UserRole.Technician,
            "admin" => Role >= UserRole.Admin,
            "test_printer" => Role >= UserRole.Technician,
            "clear_queue" => Role >= UserRole.Technician,
            "manage_users" => Role >= UserRole.Admin,
            "manage_sites" => Role >= UserRole.Admin,
            "manage_credentials" => Role >= UserRole.Admin,
            "view_audit_log" => Role >= UserRole.Admin,
            _ => false
        };
    }
}