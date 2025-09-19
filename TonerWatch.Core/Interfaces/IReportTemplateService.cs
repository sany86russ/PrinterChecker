using TonerWatch.Core.Models;

namespace TonerWatch.Core.Interfaces;

/// <summary>
/// Service interface for report template management
/// </summary>
public interface IReportTemplateService
{
    /// <summary>
    /// Get all report templates
    /// </summary>
    Task<IEnumerable<ReportTemplate>> GetReportTemplatesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get report templates by format
    /// </summary>
    Task<IEnumerable<ReportTemplate>> GetReportTemplatesByFormatAsync(string format, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get report template by ID
    /// </summary>
    Task<ReportTemplate?> GetReportTemplateByIdAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get default report template for a format
    /// </summary>
    Task<ReportTemplate?> GetDefaultTemplateAsync(string format, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create or update report template
    /// </summary>
    Task<ReportTemplate> SaveReportTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete report template
    /// </summary>
    Task<bool> DeleteReportTemplateAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set template as default for its format
    /// </summary>
    Task<bool> SetDefaultTemplateAsync(int id, CancellationToken cancellationToken = default);
}