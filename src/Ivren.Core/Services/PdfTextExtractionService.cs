using Ivren.Core.Contracts;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class PdfTextExtractionService : ITextExtractionService
{
    public TextExtractionResult Extract(PdfAnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var tokens = analysisResult.TextTokens
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var messages = new List<string> { $"Text extraction produced {tokens.Length} token(s)." };

        return new TextExtractionResult(tokens, string.Join(Environment.NewLine, tokens), messages);
    }
}
