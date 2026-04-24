using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class FileRenameService : IFileRenameService
{
    public FileRenameResult Rename(string sourceFilePath, string targetFileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(targetFileNameWithoutExtension))
        {
            return new FileRenameResult(false, false, null, "The sanitized target file name was empty.");
        }

        var sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return new FileRenameResult(false, false, null, "The source directory could not be determined.");
        }

        return RenameToFolder(sourceFilePath, sourceDirectory, targetFileNameWithoutExtension);
    }

    public FileRenameResult RenameToFolder(
        string sourceFilePath,
        string targetDirectoryPath,
        string targetFileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(targetDirectoryPath))
        {
            return new FileRenameResult(false, false, null, "The target directory was not configured.");
        }

        if (!Directory.Exists(targetDirectoryPath))
        {
            return new FileRenameResult(false, false, null, $"The target directory does not exist: {targetDirectoryPath}");
        }

        if (string.IsNullOrWhiteSpace(targetFileNameWithoutExtension))
        {
            return new FileRenameResult(false, false, null, "The sanitized target file name was empty.");
        }

        var targetFilePath = BuildTargetPath(sourceFilePath, targetDirectoryPath, targetFileNameWithoutExtension);
        if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(targetFilePath), StringComparison.OrdinalIgnoreCase))
        {
            return new FileRenameResult(true, false, targetFilePath, "The file already has the desired name.");
        }

        File.Move(sourceFilePath, targetFilePath);
        return new FileRenameResult(true, true, targetFilePath, "File renamed and moved successfully.");
    }

    public FileRenameResult MoveToFolderPreservingName(string sourceFilePath, string targetDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(targetDirectoryPath))
        {
            return new FileRenameResult(false, false, null, "The failed-files target directory was not configured.");
        }

        if (!Directory.Exists(targetDirectoryPath))
        {
            return new FileRenameResult(false, false, null, $"The failed-files target directory does not exist: {targetDirectoryPath}");
        }

        var targetFilePath = BuildTargetPathPreservingName(sourceFilePath, targetDirectoryPath);
        if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(targetFilePath), StringComparison.OrdinalIgnoreCase))
        {
            return new FileRenameResult(true, false, targetFilePath, "The failed file is already in the target folder.");
        }

        File.Move(sourceFilePath, targetFilePath);
        return new FileRenameResult(true, true, targetFilePath, "Failed file moved successfully.");
    }

    public string BuildTargetPath(
        string sourceFilePath,
        string targetDirectoryPath,
        string targetFileNameWithoutExtension)
    {
        var extension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        var targetFileName = targetFileNameWithoutExtension + extension;
        return BuildUniqueTargetPath(sourceFilePath, targetDirectoryPath, targetFileName);
    }

    public string BuildTargetPathPreservingName(string sourceFilePath, string targetDirectoryPath)
    {
        var targetFileName = Path.GetFileName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            targetFileName = "failed.pdf";
        }

        return BuildUniqueTargetPath(sourceFilePath, targetDirectoryPath, targetFileName);
    }

    private static string BuildUniqueTargetPath(string sourceFilePath, string targetDirectoryPath, string targetFileName)
    {
        var targetFilePath = Path.Combine(targetDirectoryPath, targetFileName);
        if (!File.Exists(targetFilePath)
            || string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(targetFilePath), StringComparison.OrdinalIgnoreCase))
        {
            return targetFilePath;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFileName);
        var extension = Path.GetExtension(targetFileName);

        for (var index = 1; ; index++)
        {
            var uniqueFileName = $"{fileNameWithoutExtension} ({index}){extension}";
            var uniqueFilePath = Path.Combine(targetDirectoryPath, uniqueFileName);
            if (!File.Exists(uniqueFilePath))
            {
                return uniqueFilePath;
            }
        }
    }
}
