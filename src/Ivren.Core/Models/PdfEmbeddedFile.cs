namespace Ivren.Core.Models;

public sealed record PdfEmbeddedFile(
    string FileName,
    string? MediaType,
    byte[] Content);
