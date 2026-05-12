using System.Text.Json;

namespace Ivren.Service;

public sealed class ServiceSettings
{
    public const string FileName = "Ivren.Service.settings.json";

    public string InputFolder { get; init; } = @"D:\DATA\ivren\input";

    public string RenamedFolder { get; init; } = @"D:\DATA\ivren\renamed";

    public string FailedFolder { get; init; } = @"D:\DATA\ivren\failed";

    public string AuditLogFolder { get; init; } = @"D:\DATA\ivren\logs";

    public int PollIntervalSeconds { get; init; } = 30;

    public int FileReadyDelaySeconds { get; init; } = 10;

    public bool DryRun { get; init; }

    public TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(1, PollIntervalSeconds));

    public TimeSpan FileReadyDelay => TimeSpan.FromSeconds(Math.Max(0, FileReadyDelaySeconds));

    public static ServiceSettings Load(string settingsPath, out string? message)
    {
        message = null;
        if (!File.Exists(settingsPath))
        {
            message = $"Settings file was not found: {settingsPath}. Built-in defaults will be used.";
            return new ServiceSettings();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<ServiceSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return settings ?? new ServiceSettings();
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException)
        {
            message = $"Settings file could not be loaded: {settingsPath}. {exception.Message} Built-in defaults will be used.";
            return new ServiceSettings();
        }
    }
}

public sealed record ServiceRuntimePaths(
    string AppBaseDirectory,
    string SettingsPath,
    string SupplierProfilesPath);
