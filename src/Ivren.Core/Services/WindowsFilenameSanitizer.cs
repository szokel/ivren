using System.Text;
using Ivren.Core.Contracts;

namespace Ivren.Core.Services;

public sealed class WindowsFilenameSanitizer : IFilenameSanitizer
{
    private readonly HashSet<char> _invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();

    public string Sanitize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(_invalidCharacters.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.', ' ');
        while (sanitized.Contains("__", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        }

        return sanitized;
    }
}
