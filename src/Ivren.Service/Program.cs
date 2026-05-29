using Ivren.Core.Contracts;
using Ivren.Core.Services;
using Ivren.Service;

var baseDirectory = AppContext.BaseDirectory;
var settingsPath = Path.Combine(baseDirectory, ServiceSettings.FileName);
ServiceSettings settings;
try
{
    settings = ServiceSettings.Load(settingsPath);
}
catch (InvalidOperationException exception)
{
    WriteStartupFailure(baseDirectory, settingsPath, exception);
    Environment.ExitCode = 1;
    return;
}

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

await host.RunAsync();

static void WriteStartupFailure(string baseDirectory, string settingsPath, Exception exception)
{
    var message = $"Ivren service startup failed. Settings file: {settingsPath}. {exception.Message}";
    Console.Error.WriteLine(message);

    try
    {
        var logFilePath = Path.Combine(baseDirectory, $"ivren-startup-error-{DateTime.Now:yyyy-MM-dd}.log");
        File.AppendAllText(
            logFilePath,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}{exception}{Environment.NewLine}");
    }
    catch
    {
        // If the service folder is read-only, the console/SCM failure still reports the startup error.
    }
}
