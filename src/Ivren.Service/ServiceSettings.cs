using System.Text.Json;

namespace Ivren.Service;

public sealed class ServiceSettings
{
    public const string FileName = "Ivren.Service.settings.json";

    public string InputFolder { get; init; } = string.Empty;

    public string RenamedFolder { get; init; } = string.Empty;

    public IReadOnlyList<ServiceFolderPair>? FolderPairs { get; init; }

    public string FailedFolder { get; init; } = string.Empty;

    public string AuditLogFolder { get; init; } = string.Empty;

    public int PollIntervalSeconds { get; init; } = 30;

    public int FileReadyDelaySeconds { get; init; } = 10;

    public bool DryRun { get; init; }

    public TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(1, PollIntervalSeconds));

    public TimeSpan FileReadyDelay => TimeSpan.FromSeconds(Math.Max(0, FileReadyDelaySeconds));

    public IReadOnlyList<ServiceFolderPair> GetFolderPairs()
        => FolderPairs is { Count: > 0 }
            ? FolderPairs
            : [new ServiceFolderPair("Default", InputFolder, RenamedFolder)];

    public static ServiceSettings Load(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            throw new InvalidOperationException($"Settings file was not found: {settingsPath}.");
        }

        ServiceSettings? settings;
        try
        {
            var json = File.ReadAllText(settingsPath);
            settings = JsonSerializer.Deserialize<ServiceSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception exception) when (exception is JsonException
            or IOException
            or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Settings file could not be loaded: {settingsPath}. {exception.Message}", exception);
        }

        if (settings is null)
        {
            throw new InvalidOperationException($"Settings file is empty or invalid: {settingsPath}.");
        }

        var validationErrors = settings.Validate().ToArray();
        if (validationErrors.Length > 0)
        {
            throw new InvalidOperationException(
                $"Settings file is invalid: {settingsPath}. {string.Join(" ", validationErrors)}");
        }

        return settings;
    }

    private IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(FailedFolder))
        {
            yield return "FailedFolder is required.";
        }

        if (string.IsNullOrWhiteSpace(AuditLogFolder))
        {
            yield return "AuditLogFolder is required.";
        }

        var folderPairs = FolderPairs;
        if (folderPairs is not { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(InputFolder))
            {
                yield return "InputFolder is required when FolderPairs is empty.";
            }

            if (string.IsNullOrWhiteSpace(RenamedFolder))
            {
                yield return "RenamedFolder is required when FolderPairs is empty.";
            }

            yield break;
        }

        var companyCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < folderPairs.Count; index++)
        {
            var settingName = $"FolderPairs[{index}]";
            ServiceFolderPair? pair = folderPairs[index];
            if (pair is null)
            {
                yield return $"{settingName} is required.";
                continue;
            }

            var companyCode = pair.CompanyCode?.Trim();
            if (string.IsNullOrWhiteSpace(companyCode))
            {
                yield return $"{settingName}.CompanyCode is required.";
            }
            else if (!companyCodes.Add(companyCode))
            {
                yield return $"{settingName}.CompanyCode is duplicated: {pair.CompanyCode}.";
            }

            if (string.IsNullOrWhiteSpace(pair.InputFolder))
            {
                yield return $"{settingName}.InputFolder is required.";
            }

            if (string.IsNullOrWhiteSpace(pair.RenamedFolder))
            {
                yield return $"{settingName}.RenamedFolder is required.";
            }
        }
    }
}

public sealed record ServiceFolderPair(
    string CompanyCode,
    string InputFolder,
    string RenamedFolder);

public sealed record ServiceRuntimePaths(
    string AppBaseDirectory,
    string SettingsPath,
    string SupplierProfilesPath);
