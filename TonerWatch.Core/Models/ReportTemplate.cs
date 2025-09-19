using System.ComponentModel.DataAnnotations;

namespace TonerWatch.Core.Models;

/// <summary>
/// Report template for customizable exports
/// </summary>
public class ReportTemplate
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Format { get; set; } = "PDF"; // PDF, CSV, Excel, HTML, etc.
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public bool IsDefault { get; set; } = false;
    
    // JSON configuration for the template
    public string Configuration { get; set; } = "{}";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public int? SiteId { get; set; }
    public virtual Site? Site { get; set; }
}