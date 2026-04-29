using System.Text;
using Ivren.Core.Contracts;

namespace Ivren.Core.Services;

public sealed class WindowsFilenameSanitizer : IFilenameSanitizer
{
    private const char InvalidFilenameCharacterReplacement = '~';
    private readonly HashSet<char> _invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();

    public string Sanitize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(_invalidCharacters.Contains(character) ? InvalidFilenameCharacterReplacement : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.', ' ');
        while (sanitized.Contains("~~", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("~~", "~", StringComparison.Ordinal);
        }

        return sanitized;
    }
}
