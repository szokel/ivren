using Ivren.Core.Models;

namespace Ivren.Core.Contracts;

public interface IPdfAnalysisService
{
    PdfAnalysisResult Analyze(string filePath);
}
