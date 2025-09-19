using System.Text;
using System.Text.Json;
using System.Data;
using System.Globalization;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OfficeOpenXml;
using System.Drawing;

namespace TonerWatch.Desktop.Services;

/// <summary>
/// Service for exporting device and supply data to various formats
/// </summary>
public class ExportService
{
    private readonly TonerWatchDbContext _context;
    private readonly IReportTemplateService _reportTemplateService;

    public ExportService(TonerWatchDbContext context, IReportTemplateService reportTemplateService)
    {
        _context = context;
        _reportTemplateService = reportTemplateService;
        
        // Initialize EPPlus license context (for non-commercial use)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Export device data to CSV format
    /// </summary>
    public async Task<string> ExportToCsvAsync()
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("Device Name,IP Address,Location,Status,Last Seen, Vendor, Model, Supplies");

        // Data rows
        foreach (var device in devices)
        {
            var supplies = string.Join("; ", device.Supplies.Select(s => 
                $"{s.Name}: {s.Percent?.ToString("F1", CultureInfo.InvariantCulture) ?? "N/A"}%"));
            
            csv.AppendLine($"\"{device.Hostname}\",\"{device.IpAddress}\",\"{device.Location}\"," +
                          $"\"{device.Status}\",\"{device.LastSeen:yyyy-MM-dd HH:mm:ss}\"," +
                          $"\"{device.Vendor}\",\"{device.Model}\",\"{supplies}\"");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Export device data to JSON format
    /// </summary>
    public async Task<string> ExportToJsonAsync()
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        var exportData = devices.Select(device => new
        {
            Name = device.Hostname,
            IpAddress = device.IpAddress,
            Location = device.Location,
            Status = device.Status.ToString(),
            LastSeen = device.LastSeen,
            Vendor = device.Vendor,
            Model = device.Model,
            Supplies = device.Supplies.Select(supply => new
            {
                Name = supply.Name,
                Kind = supply.Kind.ToString(),
                Percent = supply.Percent,
                LevelRaw = supply.LevelRaw,
                MaxRaw = supply.MaxRaw,
                PartNumber = supply.PartNumber,
                UpdatedAt = supply.UpdatedAt
            }).ToList()
        }).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(exportData, options);
    }

    /// <summary>
    /// Export device data to XML format
    /// </summary>
    public async Task<string> ExportToXmlAsync()
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine("<Devices>");

        foreach (var device in devices)
        {
            xml.AppendLine("  <Device>");
            xml.AppendLine($"    <Name>{EscapeXml(device.Hostname)}</Name>");
            xml.AppendLine($"    <IpAddress>{EscapeXml(device.IpAddress)}</IpAddress>");
            xml.AppendLine($"    <Location>{EscapeXml(device.Location ?? "")}</Location>");
            xml.AppendLine($"    <Status>{device.Status}</Status>");
            xml.AppendLine($"    <LastSeen>{device.LastSeen:yyyy-MM-ddTHH:mm:ss}</LastSeen>");
            xml.AppendLine($"    <Vendor>{EscapeXml(device.Vendor ?? "")}</Vendor>");
            xml.AppendLine($"    <Model>{EscapeXml(device.Model ?? "")}</Model>");
            xml.AppendLine("    <Supplies>");

            foreach (var supply in device.Supplies)
            {
                xml.AppendLine("      <Supply>");
                xml.AppendLine($"        <Name>{EscapeXml(supply.Name ?? "")}</Name>");
                xml.AppendLine($"        <Kind>{supply.Kind}</Kind>");
                xml.AppendLine($"        <Percent>{supply.Percent?.ToString(CultureInfo.InvariantCulture) ?? ""}</Percent>");
                xml.AppendLine($"        <LevelRaw>{supply.LevelRaw?.ToString() ?? ""}</LevelRaw>");
                xml.AppendLine($"        <MaxRaw>{supply.MaxRaw?.ToString() ?? ""}</MaxRaw>");
                xml.AppendLine($"        <PartNumber>{EscapeXml(supply.PartNumber ?? "")}</PartNumber>");
                xml.AppendLine($"        <UpdatedAt>{supply.UpdatedAt:yyyy-MM-ddTHH:mm:ss}</UpdatedAt>");
                xml.AppendLine("      </Supply>");
            }

            xml.AppendLine("    </Supplies>");
            xml.AppendLine("  </Device>");
        }

        xml.AppendLine("</Devices>");

        return xml.ToString();
    }

    /// <summary>
    /// Export device data to HTML format
    /// </summary>
    public async Task<string> ExportToHtmlAsync()
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("    <title>TonerWatch Device Export</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("        table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("        th { background-color: #f2f2f2; }");
        html.AppendLine("        .supply-low { color: #ff6b6b; }");
        html.AppendLine("        .supply-medium { color: #ffa500; }");
        html.AppendLine("        .supply-good { color: #28a745; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<h1>TonerWatch Device Export</h1>");
        html.AppendLine("<p>Exported on: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</p>");
        html.AppendLine("<table>");
        html.AppendLine("    <thead>");
        html.AppendLine("        <tr>");
        html.AppendLine("            <th>Device Name</th>");
        html.AppendLine("            <th>IP Address</th>");
        html.AppendLine("            <th>Location</th>");
        html.AppendLine("            <th>Status</th>");
        html.AppendLine("            <th>Last Seen</th>");
        html.AppendLine("            <th>Vendor</th>");
        html.AppendLine("            <th>Model</th>");
        html.AppendLine("            <th>Supplies</th>");
        html.AppendLine("        </tr>");
        html.AppendLine("    </thead>");
        html.AppendLine("    <tbody>");

        foreach (var device in devices)
        {
            html.AppendLine("        <tr>");
            html.AppendLine($"            <td>{EscapeHtml(device.Hostname)}</td>");
            html.AppendLine($"            <td>{EscapeHtml(device.IpAddress)}</td>");
            html.AppendLine($"            <td>{EscapeHtml(device.Location ?? "")}</td>");
            html.AppendLine($"            <td>{device.Status}</td>");
            html.AppendLine($"            <td>{device.LastSeen:yyyy-MM-dd HH:mm:ss}</td>");
            html.AppendLine($"            <td>{EscapeHtml(device.Vendor ?? "")}</td>");
            html.AppendLine($"            <td>{EscapeHtml(device.Model ?? "")}</td>");
            html.AppendLine("            <td>");

            foreach (var supply in device.Supplies)
            {
                var percent = supply.Percent ?? 0;
                var cssClass = percent <= 15 ? "supply-low" : percent <= 30 ? "supply-medium" : "supply-good";
                html.AppendLine($"                <div class=\"{cssClass}\">{EscapeHtml(supply.Name ?? "")}: {percent:F1}%</div>");
            }

            html.AppendLine("            </td>");
            html.AppendLine("        </tr>");
        }

        html.AppendLine("    </tbody>");
        html.AppendLine("</table>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Export device data to PDF format with professional formatting
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync(string? templateName = null)
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                // Page size and margins
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                
                // Header
                page.Header().Element(ComposeHeader);
                
                // Content
                page.Content().Element(container =>
                {
                    container.PaddingVertical(10).Column(column =>
                    {
                        column.Item().Text($"TonerWatch Device Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Medium);
                        
                        column.Item().PaddingTop(10).Text($"Total Devices: {devices.Count}")
                            .FontSize(12);
                        
                        // Device table
                        column.Item().PaddingTop(20).Element(container =>
                        {
                            container.Table(table =>
                            {
                                // Table header
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2); // Device Name
                                    columns.RelativeColumn(1); // IP
                                    columns.RelativeColumn(1); // Location
                                    columns.RelativeColumn(1); // Status
                                    columns.RelativeColumn(1); // Supplies
                                });
                                
                                // Header row
                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Device Name").SemiBold();
                                    header.Cell().Element(CellStyle).Text("IP Address").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Location").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Status").SemiBold();
                                    header.Cell().Element(CellStyle).Text("Supplies").SemiBold();
                                    
                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                    }
                                });
                                
                                // Data rows
                                foreach (var device in devices)
                                {
                                    table.Cell().Element(CellStyle).Text(device.Hostname);
                                    table.Cell().Element(CellStyle).Text(device.IpAddress ?? "N/A");
                                    table.Cell().Element(CellStyle).Text(device.Location ?? "N/A");
                                    table.Cell().Element(CellStyle).Text(device.Status.ToString());
                                    
                                    // Supplies column
                                    var suppliesText = string.Join("\n", device.Supplies.Select(s => 
                                        $"{s.Name}: {s.Percent?.ToString("F1") ?? "N/A"}%"));
                                    table.Cell().Element(CellStyle).Text(suppliesText);
                                    
                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1);
                                    }
                                }
                            });
                        });
                    });
                });
                
                // Footer
                page.Footer().AlignCenter().Text($"Page {{page}}")
                    .FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("TonerWatch").FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                column.Item().Text("Printer Supply Monitoring Report").FontSize(14).FontColor(Colors.Grey.Medium);
            });
            
            row.ConstantItem(100).AlignRight().Text(DateTime.Now.ToString("yyyy-MM-dd"));
        });
    }

    /// <summary>
    /// Export device data to Excel format (XLSX) with professional formatting
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync(string? templateName = null)
    {
        var devices = await _context.Devices
            .Include(d => d.Supplies)
            .Include(d => d.Site)
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("TonerWatch Report");
        
        // Set default font
        worksheet.Cells.Style.Font.Name = "Calibri";
        worksheet.Cells.Style.Font.Size = 11;
        
        // Header row
        worksheet.Cells[1, 1].Value = "Device Name";
        worksheet.Cells[1, 2].Value = "IP Address";
        worksheet.Cells[1, 3].Value = "Location";
        worksheet.Cells[1, 4].Value = "Status";
        worksheet.Cells[1, 5].Value = "Last Seen";
        worksheet.Cells[1, 6].Value = "Vendor";
        worksheet.Cells[1, 7].Value = "Model";
        worksheet.Cells[1, 8].Value = "Supplies";
        
        // Format header row
        using (var range = worksheet.Cells[1, 1, 1, 8])
        {
            range.Style.Font.Bold = true;
            // Set solid fill pattern for the header
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillPattern.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(79, 129, 189)); // Blue header
            range.Style.Font.Color.SetColor(Color.White);
            // Set thin border around the header
            range.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
        }
        
        // Data rows
        int row = 2;
        foreach (var device in devices)
        {
            worksheet.Cells[row, 1].Value = device.Hostname;
            worksheet.Cells[row, 2].Value = device.IpAddress ?? "N/A";
            worksheet.Cells[row, 3].Value = device.Location ?? "N/A";
            worksheet.Cells[row, 4].Value = device.Status.ToString();
            worksheet.Cells[row, 5].Value = device.LastSeen.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cells[row, 6].Value = device.Vendor ?? "N/A";
            worksheet.Cells[row, 7].Value = device.Model ?? "N/A";
            
            // Supplies column with formatting based on levels
            var supplyCell = worksheet.Cells[row, 8];
            var suppliesText = string.Join("\n", device.Supplies.Select(s => 
                $"{s.Name}: {s.Percent?.ToString("F1") ?? "N/A"}%"));
            supplyCell.Value = suppliesText;
            
            // Color coding based on supply levels
            var criticalSupplies = device.Supplies.Count(s => s.Percent <= 15);
            var warningSupplies = device.Supplies.Count(s => s.Percent > 15 && s.Percent <= 30);
            
            if (criticalSupplies > 0)
            {
                supplyCell.Style.Font.Color.SetColor(Color.Red);
            }
            else if (warningSupplies > 0)
            {
                supplyCell.Style.Font.Color.SetColor(Color.Orange);
            }
            
            // Add borders
            for (int col = 1; col <= 8; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }
            
            row++;
        }
        
        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();
        
        // Add title
        worksheet.InsertRow(1, 2);
        worksheet.Cells[1, 1].Value = "TonerWatch Device Report";
        worksheet.Cells[1, 1].Style.Font.Size = 16;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.FromArgb(79, 129, 189));
        
        worksheet.Cells[2, 1].Value = $"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        worksheet.Cells[2, 1].Style.Font.Size = 12;
        
        return await package.GetAsByteArrayAsync();
    }

    private static string EscapeXml(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("&", "&amp;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("\"", "&quot;")
                  .Replace("'", "&apos;");
    }

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("&", "&amp;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("\"", "&quot;");
    }
}