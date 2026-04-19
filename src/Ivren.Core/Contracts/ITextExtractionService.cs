using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface ITextExtractionService
{
    TextExtractionResult Extract(PdfAnalysisResult analysisResult);
}
