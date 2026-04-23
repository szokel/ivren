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

    private static readonly Regex FileSpecObjectReferenceRegex = new(
        @"/FS\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex DirectEmbeddedFileReferenceRegex = new(
        @"/EF\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex FilterArrayRegex = new(
        @"/Filter\s*\[(?<value>.*?)\]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FilterNameRegex = new(
        @"/(?<name>[A-Za-z0-9#]+)",
        RegexOptions.Compiled);

    private static readonly Regex PageContentsReferenceRegex = new(
        @"/Contents\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex PageContentsArrayRegex = new(
        @"/Contents\s*\[(?<value>.*?)\]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FontResourceRegex = new(
        @"/Font\s*<<(?<value>.*?)>>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FontAliasReferenceRegex = new(
        @"/(?<alias>F\d+)\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex ToUnicodeReferenceRegex = new(
        @"/ToUnicode\s+(?<object>\d+)\s+(?<generation>\d+)\s+R",
        RegexOptions.Compiled);

    private static readonly Regex FontSelectionRegex = new(
        @"/(?<alias>F\d+)\s+[-+]?\d*\.?\d+\s+Tf",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LiteralTextOperatorRegex = new(
        @"\((?<value>(?:\\.|[^\\)])*)\)\s*(?:Tj|'|"")",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HexTextOperatorRegex = new(
        @"<(?<value>[0-9A-Fa-f]+)>\s*Tj",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ArrayTextOperatorRegex = new(
        @"\[(?<value>.*?)\]\s*TJ",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BfCharSectionRegex = new(
        @"beginbfchar(?<value>.*?)endbfchar",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BfRangeSectionRegex = new(
        @"beginbfrange(?<value>.*?)endbfrange",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex BfCharEntryRegex = new(
        @"<(?<source>[0-9A-Fa-f]+)>\s*<(?<target>[0-9A-Fa-f]+)>",
        RegexOptions.Compiled);

    private static readonly Regex BfRangeSimpleEntryRegex = new(
        @"<(?<start>[0-9A-Fa-f]+)>\s*<(?<end>[0-9A-Fa-f]+)>\s*<(?<target>[0-9A-Fa-f]+)>",
        RegexOptions.Compiled);

    private static readonly Regex BfRangeArrayEntryRegex = new(
        @"<(?<start>[0-9A-Fa-f]+)>\s*<(?<end>[0-9A-Fa-f]+)>\s*\[(?<targets>.*?)\]",
        RegexOptions.Compiled | RegexOptions.Singleline);

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

        foreach (var pdfObject in objects.Values)
        {
            if (LooksLikeFileSpec(pdfObject.Body))
            {
                AddEmbeddedFileReferencesFromFileSpec(pdfObject.Body, objects, fileSpecs);
            }

            if (!LooksLikeFileAttachmentAnnotation(pdfObject.Body))
            {
                continue;
            }

            AddEmbeddedFileReferencesFromFileSpec(pdfObject.Body, objects, fileSpecs);

            foreach (var fileSpecObjectNumber in ReadFileSpecObjectReferenceNumbers(pdfObject.Body))
            {
                if (!objects.TryGetValue(fileSpecObjectNumber, out var fileSpecObject))
                {
                    continue;
                }

                AddEmbeddedFileReferencesFromFileSpec(fileSpecObject.Body, objects, fileSpecs);
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

    private static void AddEmbeddedFileReferencesFromFileSpec(
        string fileSpecBody,
        IReadOnlyDictionary<int, PdfObjectData> objects,
        IDictionary<int, string> fileSpecs)
    {
        var fileName = ReadPdfStringValue(fileSpecBody, "UF")
            ?? ReadPdfStringValue(fileSpecBody, "F")
            ?? ReadPdfStringValue(fileSpecBody, "Contents");

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        foreach (var objectNumber in ReadEmbeddedFileReferenceNumbers(fileSpecBody))
        {
            foreach (var resolvedObjectNumber in ResolveEmbeddedFileObjectNumbers(objectNumber, objects))
            {
                fileSpecs[resolvedObjectNumber] = fileName;
            }
        }
    }

    private static List<string> ReadTextTokens(IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var tokens = new List<string>();
        var pageObjects = objects.Values
            .Where(static x => x.Body.Contains("/Type /Page", StringComparison.Ordinal))
            .ToArray();

        foreach (var pageObject in pageObjects)
        {
            var pageFonts = ReadPageFonts(pageObject.Body, objects);

            foreach (var contentObjectNumber in ReadContentObjectNumbers(pageObject.Body))
            {
                if (!objects.TryGetValue(contentObjectNumber, out var contentObject))
                {
                    continue;
                }

                var stream = TryReadStream(contentObject, objects);
                if (stream is null)
                {
                    continue;
                }

                var decodedText = Encoding.Latin1.GetString(stream.Value.DecodedBytes);
                tokens.AddRange(ExtractPageTextTokens(decodedText, pageFonts));
            }
        }

        return tokens;
    }

    private static IReadOnlyDictionary<string, Dictionary<string, string>> ReadPageFonts(
        string pageBody,
        IReadOnlyDictionary<int, PdfObjectData> objects)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var fontDictionaryMatch = FontResourceRegex.Match(pageBody);
        if (!fontDictionaryMatch.Success)
        {
            return result;
        }

        foreach (Match match in FontAliasReferenceRegex.Matches(fontDictionaryMatch.Groups["value"].Value))
        {
            var alias = match.Groups["alias"].Value;
            var fontObjectNumber = int.Parse(match.Groups["object"].Value, CultureInfo.InvariantCulture);
            if (!objects.TryGetValue(fontObjectNumber, out var fontObject))
            {
                continue;
            }

            var toUnicodeMatch = ToUnicodeReferenceRegex.Match(fontObject.Body);
            if (!toUnicodeMatch.Success)
            {
                continue;
            }

            var toUnicodeObjectNumber = int.Parse(toUnicodeMatch.Groups["object"].Value, CultureInfo.InvariantCulture);
            if (!objects.TryGetValue(toUnicodeObjectNumber, out var toUnicodeObject))
            {
                continue;
            }

            var stream = TryReadStream(toUnicodeObject, objects);
            if (stream is null)
            {
                continue;
            }

            var cmapText = Encoding.Latin1.GetString(stream.Value.DecodedBytes);
            result[alias] = ParseToUnicodeMap(cmapText);
        }

        return result;
    }

    private static IEnumerable<int> ReadContentObjectNumbers(string pageBody)
    {
        foreach (Match match in PageContentsReferenceRegex.Matches(pageBody))
        {
            yield return int.Parse(match.Groups["object"].Value, CultureInfo.InvariantCulture);
        }

        var arrayMatch = PageContentsArrayRegex.Match(pageBody);
        if (!arrayMatch.Success)
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(arrayMatch.Groups["value"].Value, @"(?<object>\d+)\s+(?<generation>\d+)\s+R"))
        {
            yield return int.Parse(match.Groups["object"].Value, CultureInfo.InvariantCulture);
        }
    }

    private static bool LooksLikeFileSpec(string body)
        => Regex.IsMatch(body, @"/Type\s*/Filespec\b", RegexOptions.Singleline);

    private static bool LooksLikeFileAttachmentAnnotation(string body)
        => Regex.IsMatch(body, @"/Subtype\s*/FileAttachment\b", RegexOptions.Singleline);

    private static IReadOnlyList<string> ExtractPageTextTokens(
        string decodedStream,
        IReadOnlyDictionary<string, Dictionary<string, string>> fontMaps)
    {
        if (!LooksLikeTextContentStream(decodedStream))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        var currentFontAlias = string.Empty;

        var operationRegex = new Regex(
            @"/(?<fontAlias>F\d+)\s+[-+]?\d*\.?\d+\s+Tf|\((?<literal>(?:\\.|[^\\)])*)\)\s*(?<literalOperator>Tj|'|"")|<(?<hex>[0-9A-Fa-f]+)>\s*Tj|\[(?<array>.*?)\]\s*TJ",
            RegexOptions.Compiled | RegexOptions.Singleline);

        foreach (Match match in operationRegex.Matches(decodedStream))
        {
            if (match.Groups["fontAlias"].Success)
            {
                currentFontAlias = match.Groups["fontAlias"].Value;
                continue;
            }

            if (match.Groups["literal"].Success)
            {
                PdfTextTokenExtractor.AddToken(tokens, PdfTextTokenExtractor.DecodePdfLiteralString(match.Groups["literal"].Value));
                continue;
            }

            if (match.Groups["hex"].Success)
            {
                PdfTextTokenExtractor.AddToken(tokens, DecodeHexText(match.Groups["hex"].Value, currentFontAlias, fontMaps));
                continue;
            }

            if (match.Groups["array"].Success)
            {
                AddArrayTextTokens(tokens, match.Groups["array"].Value, currentFontAlias, fontMaps);
            }
        }

        return tokens;
    }

    private static void AddArrayTextTokens(
        ICollection<string> tokens,
        string arrayValue,
        string currentFontAlias,
        IReadOnlyDictionary<string, Dictionary<string, string>> fontMaps)
    {
        foreach (Match literal in LiteralTextOperatorRegex.Matches(arrayValue))
        {
            PdfTextTokenExtractor.AddToken(tokens, PdfTextTokenExtractor.DecodePdfLiteralString(literal.Groups["value"].Value));
        }

        foreach (Match hex in Regex.Matches(arrayValue, @"<(?<value>[0-9A-Fa-f]+)>"))
        {
            PdfTextTokenExtractor.AddToken(tokens, DecodeHexText(hex.Groups["value"].Value, currentFontAlias, fontMaps));
        }
    }

    private static string DecodeHexText(
        string hexValue,
        string currentFontAlias,
        IReadOnlyDictionary<string, Dictionary<string, string>> fontMaps)
    {
        if (fontMaps.TryGetValue(currentFontAlias, out var unicodeMap) && unicodeMap.Count > 0)
        {
            return DecodeWithUnicodeMap(hexValue, unicodeMap);
        }

        return PdfTextTokenExtractor.DecodePdfHexString(hexValue);
    }

    private static Dictionary<string, string> ParseToUnicodeMap(string cmapText)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match section in BfCharSectionRegex.Matches(cmapText))
        {
            foreach (Match entry in BfCharEntryRegex.Matches(section.Groups["value"].Value))
            {
                map[entry.Groups["source"].Value.ToUpperInvariant()] = DecodeUnicodeHex(entry.Groups["target"].Value);
            }
        }

        foreach (Match section in BfRangeSectionRegex.Matches(cmapText))
        {
            foreach (Match entry in BfRangeSimpleEntryRegex.Matches(section.Groups["value"].Value))
            {
                var start = Convert.ToInt32(entry.Groups["start"].Value, 16);
                var end = Convert.ToInt32(entry.Groups["end"].Value, 16);
                var target = Convert.ToInt32(entry.Groups["target"].Value, 16);
                var sourceWidth = entry.Groups["start"].Value.Length;
                var targetWidth = entry.Groups["target"].Value.Length;

                for (var source = start; source <= end; source++)
                {
                    var sourceHex = source.ToString($"X{sourceWidth}", CultureInfo.InvariantCulture);
                    var targetHex = target.ToString($"X{targetWidth}", CultureInfo.InvariantCulture);
                    map[sourceHex] = DecodeUnicodeHex(targetHex);
                    target++;
                }
            }

            foreach (Match entry in BfRangeArrayEntryRegex.Matches(section.Groups["value"].Value))
            {
                var start = Convert.ToInt32(entry.Groups["start"].Value, 16);
                var targets = Regex.Matches(entry.Groups["targets"].Value, @"<(?<target>[0-9A-Fa-f]+)>")
                    .Select(static x => x.Groups["target"].Value)
                    .ToArray();

                for (var index = 0; index < targets.Length; index++)
                {
                    var sourceHex = (start + index).ToString($"X{entry.Groups["start"].Value.Length}", CultureInfo.InvariantCulture);
                    map[sourceHex] = DecodeUnicodeHex(targets[index]);
                }
            }
        }

        return map;
    }

    private static string DecodeWithUnicodeMap(string hexValue, IReadOnlyDictionary<string, string> unicodeMap)
    {
        var keyLengths = unicodeMap.Keys
            .Select(static x => x.Length)
            .Distinct()
            .OrderByDescending(static x => x)
            .ToArray();

        var normalizedHex = hexValue.ToUpperInvariant();
        var builder = new StringBuilder();

        for (var index = 0; index < normalizedHex.Length;)
        {
            var matched = false;

            foreach (var keyLength in keyLengths)
            {
                if (index + keyLength > normalizedHex.Length)
                {
                    continue;
                }

                var segment = normalizedHex.Substring(index, keyLength);
                if (!unicodeMap.TryGetValue(segment, out var value))
                {
                    continue;
                }

                builder.Append(value);
                index += keyLength;
                matched = true;
                break;
            }

            if (matched)
            {
                continue;
            }

            var fallbackLength = Math.Min(2, normalizedHex.Length - index);
            builder.Append(PdfTextTokenExtractor.DecodePdfHexString(normalizedHex.Substring(index, fallbackLength)));
            index += fallbackLength;
        }

        return builder.ToString();
    }

    private static string DecodeUnicodeHex(string hexValue)
    {
        if (hexValue.Length % 4 != 0)
        {
            return PdfTextTokenExtractor.DecodePdfHexString(hexValue);
        }

        var bytes = new byte[hexValue.Length / 2];
        for (var index = 0; index < hexValue.Length; index += 2)
        {
            bytes[index / 2] = byte.Parse(hexValue.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return Encoding.BigEndianUnicode.GetString(bytes);
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

    private static IEnumerable<int> ReadFileSpecObjectReferenceNumbers(string body)
    {
        foreach (Match reference in FileSpecObjectReferenceRegex.Matches(body))
        {
            yield return int.Parse(reference.Groups["object"].Value, CultureInfo.InvariantCulture);
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
        var literalMatch = Regex.Match(body, $@"/{key}\s*\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline);
        if (literalMatch.Success)
        {
            return DecodePdfTextString(PdfTextTokenExtractor.DecodePdfLiteralString(literalMatch.Groups["value"].Value));
        }

        var hexMatch = Regex.Match(body, $@"/{key}\s*<(?<value>[0-9A-Fa-f]+)>", RegexOptions.Singleline);
        return hexMatch.Success
            ? DecodePdfTextString(PdfTextTokenExtractor.DecodePdfHexString(hexMatch.Groups["value"].Value))
            : null;
    }

    private static string DecodePdfTextString(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var bytes = value.Select(static character => (byte)character).ToArray();
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return value;
    }

    private static string? ReadNameValue(string body, string key)
    {
        var match = Regex.Match(body, $@"/{key}\s*/(?<value>[^\s>/\]]+)", RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static bool LooksLikeTextContentStream(string value)
        => value.Contains("BT", StringComparison.Ordinal)
            && value.Contains("ET", StringComparison.Ordinal)
            && (value.Contains("Tf", StringComparison.Ordinal)
                || value.Contains("Tj", StringComparison.Ordinal)
                || value.Contains("TJ", StringComparison.Ordinal)
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
        var filters = ReadFilters(body);
        if (filters.Count == 0)
        {
            return rawBytes;
        }

        var currentBytes = rawBytes;
        foreach (var filter in filters)
        {
            try
            {
                currentBytes = filter switch
                {
                    "FlateDecode" => DecodeFlate(currentBytes),
                    "ASCII85Decode" => DecodeAscii85(currentBytes),
                    _ => currentBytes
                };
            }
            catch (InvalidDataException)
            {
                return rawBytes;
            }
            catch (FormatException)
            {
                return rawBytes;
            }
        }

        return currentBytes;
    }

    private static IReadOnlyList<string> ReadFilters(string body)
    {
        var arrayMatch = FilterArrayRegex.Match(body);
        if (arrayMatch.Success)
        {
            return FilterNameRegex.Matches(arrayMatch.Groups["value"].Value)
                .Select(static x => x.Groups["name"].Value)
                .ToArray();
        }

        var singleMatch = Regex.Match(body, @"/Filter\s*/(?<name>[A-Za-z0-9#]+)", RegexOptions.Singleline);
        if (singleMatch.Success)
        {
            return [singleMatch.Groups["name"].Value];
        }

        return Array.Empty<string>();
    }

    private static byte[] DecodeFlate(byte[] rawBytes)
    {
        using var input = new MemoryStream(rawBytes);
        using var output = new MemoryStream();
        using var zlibStream = new ZLibStream(input, CompressionMode.Decompress);
        zlibStream.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecodeAscii85(byte[] rawBytes)
    {
        var data = Encoding.ASCII.GetString(rawBytes);
        var cleaned = new StringBuilder(data.Length);

        foreach (var character in data)
        {
            if (!char.IsWhiteSpace(character))
            {
                cleaned.Append(character);
            }
        }

        var text = cleaned.ToString();
        var endIndex = text.IndexOf("~>", StringComparison.Ordinal);
        if (endIndex >= 0)
        {
            text = text[..endIndex];
        }

        using var output = new MemoryStream();
        var chunk = new List<char>(5);

        foreach (var character in text)
        {
            if (character == 'z')
            {
                if (chunk.Count != 0)
                {
                    throw new FormatException("Invalid ASCII85 data.");
                }

                output.Write([0, 0, 0, 0]);
                continue;
            }

            if (character is < '!' or > 'u')
            {
                continue;
            }

            chunk.Add(character);
            if (chunk.Count != 5)
            {
                continue;
            }

            WriteAscii85Chunk(output, chunk, 4);
            chunk.Clear();
        }

        if (chunk.Count > 0)
        {
            var originalCount = chunk.Count;
            while (chunk.Count < 5)
            {
                chunk.Add('u');
            }

            WriteAscii85Chunk(output, chunk, originalCount - 1);
        }

        return output.ToArray();
    }

    private static void WriteAscii85Chunk(Stream output, IReadOnlyList<char> chunk, int bytesToWrite)
    {
        uint value = 0;
        for (var index = 0; index < 5; index++)
        {
            value = checked(value * 85 + (uint)(chunk[index] - '!'));
        }

        Span<byte> buffer = stackalloc byte[4];
        buffer[0] = (byte)(value >> 24);
        buffer[1] = (byte)(value >> 16);
        buffer[2] = (byte)(value >> 8);
        buffer[3] = (byte)value;

        for (var index = 0; index < bytesToWrite; index++)
        {
            output.WriteByte(buffer[index]);
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
