namespace Ivren.Core.Models;

public sealed record PdfAnalysisResult(
    string FilePath,
    IReadOnlyList<PdfEmbeddedFile> EmbeddedFiles,
    IReadOnlyList<string> TextTokens,
    IReadOnlyList<string> Messages)
{
    public bool HasSelectableText => TextTokens.Count > 0;
}
