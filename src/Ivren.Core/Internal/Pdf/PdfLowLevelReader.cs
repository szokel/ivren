using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Ivren.Core.Internal.Pdf;

internal sealed class PdfLowLevelReader
{
    private static readonly Regex ObjectRegex = new(
        @"(?ms)(?<number>\d+)\s+(?<generation>\d+)\s+obj\b(?<body>.*?)\bendobj\b",
        RegexOptions.Compiled);

    private static readonly Regex LengthRegex = new(
        @"/Length\s+(?:(?<refObject>\d+)\s+(?<refGeneration>\d+)\s+R|(?<length>\d+))",
        RegexOptions.Compiled);

    private static readonly Regex FileSpecReferenceRegex = new(
        @"/(?<key>F|UF)\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex DirectEmbeddedFileReferenceRegex = new(
        @"/EF\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    public PdfReadResult Read(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var content = Encoding.Latin1.GetString(bytes);
        var objects = ReadObjects(content, bytes);

        var embeddedFiles = ReadEmbeddedFiles(objects);
        var textTokens = ReadTextTokens(objects);

        var messages = new List<string>
        {
            $"PDF analysis completed. Embedded files: {embeddedFiles.Count}, text tokens: {textTokens.Count}."
        };

        if (textTokens.Count == 0)
        {
            messages.Add("No selectable PDF text tokens were extracted. OCR may be needed for image-only PDFs later.");
        }

        return new PdfReadResult(embeddedFiles, textTokens, messages);
    }

    private static Dictionary<int, PdfObjectData> ReadObjects(string content, byte[] bytes)
    {
        var result = new Dictionary<int, PdfObjectData>();

        foreach (Match match in ObjectRegex.Matches(content))
        {
            var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            var generation = int.Parse(match.Groups["generation"].Value, CultureInfo.InvariantCulture);
            var bodyGroup = match.Groups["body"];
            var bodyBytes = new byte[bodyGroup.Length];
            Buffer.BlockCopy(bytes, bodyGroup.Index, bodyBytes, 0, bodyGroup.Length);

            result[number] = new PdfObjectData(number, generation, bodyGroup.Value, bodyBytes);
        }

        return result;
    }

    private static List<PdfEmbeddedFileData> ReadEmbeddedFiles(IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var fileSpecs = new Dictionary<int, string>(capacity: objects.Count);

        foreach (var pdfObject in objects.Values.Where(static x => x.Body.Contains("/Type /Filespec", StringComparison.Ordinal)))
        {
            var fileName = ReadPdfStringValue(pdfObject.Body, "UF")
                ?? ReadPdfStringValue(pdfObject.Body, "F");

            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            foreach (var objectNumber in ReadEmbeddedFileReferenceNumbers(pdfObject.Body))
            {
                foreach (var resolvedObjectNumber in ResolveEmbeddedFileObjectNumbers(objectNumber, objects))
                {
                    fileSpecs[resolvedObjectNumber] = fileName;
                }
            }
        }

        var files = new List<PdfEmbeddedFileData>();

        foreach (var pair in fileSpecs)
        {
            if (!objects.TryGetValue(pair.Key, out var embeddedObject))
            {
                continue;
            }

            if (!embeddedObject.Body.Contains("/EmbeddedFile", StringComparison.Ordinal))
            {
                continue;
            }

            var stream = TryReadStream(embeddedObject, objects);
            if (stream is null)
            {
                continue;
            }

            files.Add(new PdfEmbeddedFileData(pair.Value, ReadNameValue(embeddedObject.Body, "Subtype"), stream.Value.DecodedBytes));
        }

        return files;
    }

    private static List<string> ReadTextTokens(IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var tokens = new List<string>();

        foreach (var pdfObject in objects.Values)
        {
            var stream = TryReadStream(pdfObject, objects);
            if (stream is null)
            {
                continue;
            }

            var decodedText = Encoding.Latin1.GetString(stream.Value.DecodedBytes);
            if (!LooksLikeTextContentStream(decodedText))
            {
                continue;
            }

            tokens.AddRange(PdfTextTokenExtractor.ExtractTokens(decodedText));
        }

        return tokens;
    }

    private static string GetEmbeddedFileDictionary(string body)
    {
        var match = Regex.Match(body, @"/EF\s*<<(?<value>.*?)>>", RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static IEnumerable<int> ReadEmbeddedFileReferenceNumbers(string body)
    {
        var embeddedFileDictionary = GetEmbeddedFileDictionary(body);
        if (!string.IsNullOrWhiteSpace(embeddedFileDictionary))
        {
            foreach (Match reference in FileSpecReferenceRegex.Matches(embeddedFileDictionary))
            {
                yield return int.Parse(reference.Groups["object"].Value, CultureInfo.InvariantCulture);
            }

            yield break;
        }

        var directReference = DirectEmbeddedFileReferenceRegex.Match(body);
        if (directReference.Success)
        {
            yield return int.Parse(directReference.Groups["object"].Value, CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<int> ResolveEmbeddedFileObjectNumbers(int objectNumber, IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        if (!objects.TryGetValue(objectNumber, out var pdfObject))
        {
            yield break;
        }

        if (pdfObject.Body.Contains("/EmbeddedFile", StringComparison.Ordinal))
        {
            yield return objectNumber;
            yield break;
        }

        foreach (Match reference in FileSpecReferenceRegex.Matches(pdfObject.Body))
        {
            yield return int.Parse(reference.Groups["object"].Value, CultureInfo.InvariantCulture);
        }
    }

    private static string? ReadPdfStringValue(string body, string key)
    {
        var match = Regex.Match(body, $@"/{key}\s+\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        return PdfTextTokenExtractor.DecodePdfLiteralString(match.Groups["value"].Value);
    }

    private static string? ReadNameValue(string body, string key)
    {
        var match = Regex.Match(body, $@"/{key}\s*/(?<value>[^\s>/\]]+)", RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static bool LooksLikeTextContentStream(string value)
        => value.Contains("BT", StringComparison.Ordinal)
            && (value.Contains("Tj", StringComparison.Ordinal)
                || value.Contains("TJ", StringComparison.Ordinal)
                || value.Contains("'", StringComparison.Ordinal)
                || value.Contains("\"", StringComparison.Ordinal));

    private static PdfStreamData? TryReadStream(PdfObjectData pdfObject, IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var streamKeywordIndex = pdfObject.Body.IndexOf("stream", StringComparison.Ordinal);
        if (streamKeywordIndex < 0)
        {
            return null;
        }

        var streamStartIndex = streamKeywordIndex + "stream".Length;
        if (streamStartIndex < pdfObject.Body.Length && pdfObject.Body[streamStartIndex] == '\r')
        {
            streamStartIndex++;
        }

        if (streamStartIndex < pdfObject.Body.Length && pdfObject.Body[streamStartIndex] == '\n')
        {
            streamStartIndex++;
        }

        var declaredLength = ResolveStreamLength(pdfObject.Body, objects);
        byte[] rawBytes;

        if (declaredLength.HasValue && declaredLength.Value >= 0 && streamStartIndex + declaredLength.Value <= pdfObject.BodyBytes.Length)
        {
            rawBytes = pdfObject.BodyBytes.AsSpan(streamStartIndex, declaredLength.Value).ToArray();
        }
        else
        {
            var endStreamIndex = pdfObject.Body.IndexOf("endstream", streamStartIndex, StringComparison.Ordinal);
            if (endStreamIndex < 0)
            {
                return null;
            }

            var rawLength = endStreamIndex - streamStartIndex;
            if (rawLength < 0)
            {
                return null;
            }

            rawBytes = pdfObject.BodyBytes.AsSpan(streamStartIndex, rawLength).ToArray();
            rawBytes = TrimTrailingLineEnding(rawBytes);
        }

        var decodedBytes = DecodeStream(rawBytes, pdfObject.Body);
        return new PdfStreamData(rawBytes, decodedBytes);
    }

    private static int? ResolveStreamLength(string body, IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var match = LengthRegex.Match(body);
        if (!match.Success)
        {
            return null;
        }

        if (match.Groups["length"].Success)
        {
            return int.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture);
        }

        if (!match.Groups["refObject"].Success)
        {
            return null;
        }

        var referencedObjectNumber = int.Parse(match.Groups["refObject"].Value, CultureInfo.InvariantCulture);
        if (!objects.TryGetValue(referencedObjectNumber, out var referencedObject))
        {
            return null;
        }

        var numericText = referencedObject.Body.Trim();
        return int.TryParse(numericText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static byte[] DecodeStream(byte[] rawBytes, string body)
    {
        if (!body.Contains("/FlateDecode", StringComparison.Ordinal))
        {
            return rawBytes;
        }

        using var input = new MemoryStream(rawBytes);
        using var output = new MemoryStream();

        try
        {
            using var zlibStream = new ZLibStream(input, CompressionMode.Decompress);
            zlibStream.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return rawBytes;
        }
    }

    private static byte[] TrimTrailingLineEnding(byte[] bytes)
    {
        var length = bytes.Length;

        while (length > 0 && (bytes[length - 1] == '\r' || bytes[length - 1] == '\n'))
        {
            length--;
        }

        if (length == bytes.Length)
        {
            return bytes;
        }

        return bytes.AsSpan(0, length).ToArray();
    }
}

internal sealed record PdfReadResult(
    IReadOnlyList<PdfEmbeddedFileData> EmbeddedFiles,
    IReadOnlyList<string> TextTokens,
    IReadOnlyList<string> Messages);

internal sealed record PdfEmbeddedFileData(
    string FileName,
    string? MediaType,
    byte[] Content);

internal sealed record PdfObjectData(
    int Number,
    int Generation,
    string Body,
    byte[] BodyBytes);

internal readonly record struct PdfStreamData(
    byte[] RawBytes,
    byte[] DecodedBytes);
