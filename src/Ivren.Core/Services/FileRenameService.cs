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

        var extension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pdf";
        }

        var targetFilePath = Path.Combine(sourceDirectory, targetFileNameWithoutExtension + extension);
        if (string.Equals(sourceFilePath, targetFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return new FileRenameResult(true, false, targetFilePath, "The file already has the desired name.");
        }

        if (File.Exists(targetFilePath))
        {
            return new FileRenameResult(false, false, targetFilePath, "A file with the target name already exists.");
        }

        File.Move(sourceFilePath, targetFilePath);
        return new FileRenameResult(true, true, targetFilePath, "File renamed successfully.");
    }
}
