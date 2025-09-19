using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Infrastructure.Services;
using TonerWatch.Discovery;
using TonerWatch.Service;
using TonerWatch.Protocols.SNMP;

var builder = Host.CreateApplicationBuilder(args);

// Configure Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TonerWatch Monitor";
});

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddEventLog();
});

// Register HTTP client
builder.Services.AddHttpClient();

// Register core services
builder.Services.AddSingleton<ISupplyNormalizationService, SupplyNormalizationService>();
builder.Services.AddSingleton<IForecastService, ForecastService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<INotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<NotificationService>>();
            var emailSender = provider.GetRequiredService<IEmailNotificationSender>();
            var telegramSender = provider.GetRequiredService<ITelegramNotificationSender>();
            var webhookSender = provider.GetRequiredService<IWebhookNotificationSender>();
            var notificationHistoryService = provider.GetRequiredService<INotificationHistoryService>();
            return new NotificationService(logger, emailSender, telegramSender, webhookSender, notificationHistoryService);
        });
        builder.Services.AddSingleton<INotificationHistoryService, NotificationHistoryService>();
        builder.Services.AddSingleton<IReportTemplateService, ReportTemplateService>();
        builder.Services.AddSingleton<IRealTimeMonitoringService, RealTimeMonitoringService>();
builder.Services.AddSingleton<INotificationHistoryService, NotificationHistoryService>();
builder.Services.AddSingleton<IEmailNotificationSender, EmailNotificationSender>();
builder.Services.AddSingleton<ITelegramNotificationSender, TelegramNotificationSender>();
builder.Services.AddSingleton<IWebhookNotificationSender, WebhookNotificationSender>();

// Register protocol services
builder.Services.AddSingleton<ISnmpProtocol, SnmpProtocolService>();
builder.Services.AddSingleton<VendorSpecificMibs>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<VendorSpecificMibs>>();
    var snmpProtocol = provider.GetRequiredService<ISnmpProtocol>();
    return new VendorSpecificMibs(logger, snmpProtocol);
});

// Register device discovery service with SNMP protocol dependency
builder.Services.AddSingleton<IDeviceDiscoveryService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<DeviceDiscoveryService>>();
    var snmpProtocol = provider.GetService<ISnmpProtocol>();
    return new DeviceDiscoveryService(logger, snmpProtocol);
});

// Register background workers
builder.Services.AddHostedService<TonerWatchWorker>();
builder.Services.AddHostedService<DeviceDiscoveryWorker>();
builder.Services.AddHostedService<SupplyMonitoringWorker>();
builder.Services.AddHostedService<AlertProcessingWorker>();
builder.Services.AddHostedService<NotificationWorker>();

var host = builder.Build();
host.Run();