using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IFileRenameService
{
    FileRenameResult Rename(string sourceFilePath, string targetFileNameWithoutExtension);

    FileRenameResult RenameToFolder(
        string sourceFilePath,
        string targetDirectoryPath,
        string targetFileNameWithoutExtension);

    FileRenameResult MoveToFolderPreservingName(string sourceFilePath, string targetDirectoryPath);

    string BuildTargetPath(
        string sourceFilePath,
        string targetDirectoryPath,
        string targetFileNameWithoutExtension);

    string BuildTargetPathPreservingName(string sourceFilePath, string targetDirectoryPath);
}
