using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IFileRenameService
{
    FileRenameResult Rename(string sourceFilePath, string targetFileNameWithoutExtension);
}
