namespace Ivren.Core.Models;

public sealed record PdfAnalysisResult(
    string FilePath,
    IReadOnlyList<PdfEmbeddedFile> EmbeddedFiles,
    IReadOnlyList<string> TextTokens,
    IReadOnlyList<string> Messages,
    bool IsEncrypted = false)
{
    public bool HasSelectableText => TextTokens.Count > 0;
}
