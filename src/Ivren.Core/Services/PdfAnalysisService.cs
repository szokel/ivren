using Ivren.Core.Contracts;
using Ivren.Core.Internal.Pdf;
using Ivren.Core.Models;

namespace Ivren.Core.Services;

public sealed class PdfAnalysisService : IPdfAnalysisService
{
    private readonly PdfLowLevelReader _pdfReader = new();

    public PdfAnalysisResult Analyze(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The PDF file could not be found.", filePath);
        }

        var readResult = _pdfReader.Read(filePath);
        var embeddedFiles = readResult.EmbeddedFiles
            .Select(static x => new PdfEmbeddedFile(x.FileName, x.MediaType, x.Content))
            .ToArray();

        return new PdfAnalysisResult(
            filePath,
            embeddedFiles,
            readResult.TextTokens,
            readResult.Messages.ToArray(),
            readResult.IsEncrypted);
    }
}
