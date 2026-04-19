namespace Ivren.Core.Models;

public sealed record FileRenameResult(
    bool Success,
    bool Renamed,
    string? TargetFilePath,
    string Message);
