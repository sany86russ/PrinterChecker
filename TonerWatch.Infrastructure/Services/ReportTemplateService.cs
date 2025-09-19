using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TonerWatch.Infrastructure.Services;

/// <summary>
/// Service for managing report templates
/// </summary>
public class ReportTemplateService : IReportTemplateService
{
    private readonly ILogger<ReportTemplateService> _logger;
    private readonly TonerWatchDbContext _context;

    public ReportTemplateService(ILogger<ReportTemplateService> logger, TonerWatchDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IEnumerable<ReportTemplate>> GetReportTemplatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all report templates");
        
        try
        {
            return await _context.ReportTemplates
                .Include(rt => rt.Site)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report templates");
            throw;
        }
    }

    public async Task<IEnumerable<ReportTemplate>> GetReportTemplatesByFormatAsync(string format, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving report templates for format {Format}", format);
        
        try
        {
            return await _context.ReportTemplates
                .Include(rt => rt.Site)
                .Where(rt => rt.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report templates for format {Format}", format);
            throw;
        }
    }

    public async Task<ReportTemplate?> GetReportTemplateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving report template {Id}", id);
        
        try
        {
            return await _context.ReportTemplates
                .Include(rt => rt.Site)
                .FirstOrDefaultAsync(rt => rt.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report template {Id}", id);
            throw;
        }
    }

    public async Task<ReportTemplate?> GetDefaultTemplateAsync(string format, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving default report template for format {Format}", format);
        
        try
        {
            return await _context.ReportTemplates
                .Include(rt => rt.Site)
                .Where(rt => rt.Format.Equals(format, StringComparison.OrdinalIgnoreCase) && rt.IsDefault)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default report template for format {Format}", format);
            throw;
        }
    }

    public async Task<ReportTemplate> SaveReportTemplateAsync(ReportTemplate template, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving report template {TemplateName}", template.Name);

        try
        {
            if (template.Id == 0)
            {
                template.CreatedAt = DateTime.UtcNow;
                _context.ReportTemplates.Add(template);
            }
            else
            {
                template.UpdatedAt = DateTime.UtcNow;
                _context.ReportTemplates.Update(template);
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Saved report template {TemplateName} with ID {TemplateId}", template.Name, template.Id);
            
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving report template {TemplateName}", template.Name);
            throw;
        }
    }

    public async Task<bool> DeleteReportTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting report template {Id}", id);

        try
        {
            var template = await _context.ReportTemplates.FindAsync(id);
            if (template == null)
            {
                _logger.LogWarning("Report template {Id} not found for deletion", id);
                return false;
            }

            _context.ReportTemplates.Remove(template);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Deleted report template {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report template {Id}", id);
            throw;
        }
    }

    public async Task<bool> SetDefaultTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting report template {Id} as default", id);

        try
        {
            var template = await _context.ReportTemplates.FindAsync(id);
            if (template == null)
            {
                _logger.LogWarning("Report template {Id} not found", id);
                return false;
            }

            // First, unset any existing default templates for this format
            var existingDefaults = await _context.ReportTemplates
                .Where(rt => rt.Format == template.Format && rt.IsDefault && rt.Id != id)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }

            // Set the new default
            template.IsDefault = true;
            template.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Set report template {Id} as default for format {Format}", id, template.Format);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting report template {Id} as default", id);
            throw;
        }
    }
}