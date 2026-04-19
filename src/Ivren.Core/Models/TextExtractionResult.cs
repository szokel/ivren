namespace Ivren.Core.Models;

public sealed record TextExtractionResult(
    IReadOnlyList<string> Tokens,
    string FullText,
    IReadOnlyList<string> Messages);
