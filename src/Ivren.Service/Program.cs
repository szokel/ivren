using Ivren.Core.Contracts;
using Ivren.Core.Services;
using Ivren.Service;

var baseDirectory = AppContext.BaseDirectory;
var settingsPath = Path.Combine(baseDirectory, ServiceSettings.FileName);
var settings = ServiceSettings.Load(settingsPath, out var settingsLoadMessage);
var supplierProfilesPath = Path.Combine(baseDirectory, "Ivren.SupplierProfiles.json");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "IvrenService";
});

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});
builder.Logging.AddProvider(new DailyFileLoggerProvider(settings.AuditLogFolder));

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(new ServiceRuntimePaths(
    baseDirectory,
    settingsPath,
    supplierProfilesPath));

builder.Services.AddSingleton<IPdfAnalysisService, PdfAnalysisService>();
builder.Services.AddSingleton<IXmlInvoiceDataExtractor, XmlInvoiceDataExtractor>();
builder.Services.AddSingleton<ITextExtractionService, PdfTextExtractionService>();
builder.Services.AddSingleton<IInvoiceNumberDetector, InvoiceNumberDetector>();
builder.Services.AddSingleton<IFilenameSanitizer, WindowsFilenameSanitizer>();
builder.Services.AddSingleton<IFileRenameService, FileRenameService>();
builder.Services.AddSingleton<IAuditLogService, JsonLinesAuditLogService>();
builder.Services.AddSingleton<ISupplierProfileProvider>(_ => new JsonSupplierProfileProvider(supplierProfilesPath));
builder.Services.AddSingleton<IInvoiceFileProcessor, InvoiceFileProcessor>();
builder.Services.AddHostedService<IvrenWorker>();

var host = builder.Build();

if (!string.IsNullOrWhiteSpace(settingsLoadMessage))
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ivren.Service.Startup");
    logger.LogWarning("{Message}", settingsLoadMessage);
}

await host.RunAsync();
