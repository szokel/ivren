# Invoice Processing Pipeline

## Purpose

This document summarizes the current invoice-processing pipeline in the `ivren` solution. It describes the processing layers, the main classes involved, the current detection priority, the PDF structures supported so far, and the main improvements made during the current implementation session.

The application is designed with these principles:

- `Ivren.Core` contains all business logic.
- `Ivren.WinForms` is a thin operator and test UI.
- Embedded XML is the primary extraction path.
- Text extraction is the secondary fallback path.
- OCR is planned for later and is not yet implemented.

## Pipeline Diagram

```text
+-------------------+
| Input PDF file    |
+-------------------+
          |
          v
+-------------------------------+
| PDF analysis / structure read |
| - objects / streams           |
| - embedded files              |
| - text tokens                 |
+-------------------------------+
          |
          v
+-------------------+
| Embedded XML path |
+-------------------+
          |
          v
+-------------------------------+
| XML discovery                 |
| - Filespec / EF              |
| - FileAttachment             |
| - ObjStm-expanded objects    |
+-------------------------------+
          |
          v
+-------------------------------+
| XML decode + parse            |
| - parse from raw bytes        |
| - respect BOM / declaration   |
+-------------------------------+
          |
          v
+-------------------------------+
| Invoice number from XML?      |
+-------------------------------+
      | yes               | no
      v                   v
+----------------+   +-------------------+
| Sanitize name  |   | Text extraction   |
+----------------+   +-------------------+
      |                   |
      |                   v
      |         +-----------------------+
      |         | Text normalization    |
      |         +-----------------------+
      |                   |
      |                   v
      |         +-----------------------+
      |         | Invoice number        |
      |         | detection from text   |
      |         +-----------------------+
      |                   |
      |             yes   |   no
      |                   v
      |           +---------------------+
      |           | Final failure       |
      |           | - no rename         |
      |           | - report reason     |
      |           +---------------------+
      |
      v
+-------------------------------+
| Rename decision               |
| - dry-run: skip rename        |
| - live: rename to invoice no. |
+-------------------------------+
```

## Detection Priority Order

Current processing order for each PDF:

1. Analyze PDF structure and discover embedded files.
2. Try embedded XML first.
3. If no usable XML invoice number is found, extract selectable text.
4. Detect invoice number from text.
5. Sanitize invoice number for Windows filenames.
6. Rename the file, or skip rename in dry-run mode.

Planned future order:

1. XML
2. Text
3. OCR

## Supported PDF Structures So Far

The current lightweight PDF parser is not a full PDF engine, but it already supports several invoice-relevant structures:

- Standalone `/Type /Filespec` objects
- Compact `/Type/Filespec` syntax
- `/F` and `/UF` filenames
- `/EF` embedded-file references
- `/Subtype/FileAttachment` annotations
- Inline Filespec dictionaries
- Embedded file streams with `/Type/EmbeddedFile`
- Object streams: `/ObjStm`
- Page content streams via `/Contents`
- Form XObjects referenced from page content via `Do`
- Font ToUnicode maps used during text extraction
- Stream decoding for `FlateDecode`
- Stream decoding for `ASCII85Decode`

## 1. PDF Structure Handling

### Purpose

Read raw PDF objects, decode streams, expand supported PDF storage structures, and expose:

- embedded file attachments
- raw selectable text tokens

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Internal\Pdf\PdfLowLevelReader.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\PdfAnalysisService.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\PdfAnalysisResult.cs`

### How It Currently Works

- Reads the whole PDF as bytes and as Latin-1 text for low-level object parsing.
- Parses top-level `obj ... endobj` objects.
- Expands `/ObjStm` object streams into in-memory objects.
- Reads embedded-file-related objects and streams.
- Reads page `/Contents` streams.
- Follows Form XObjects referenced by page content.
- Decodes supported stream filters and returns extracted embedded files plus raw text tokens.

### Known Limitations / Edge Cases

- It is intentionally limited and is **not** a full PDF engine.
- Unsupported compression/filter combinations may still block extraction.
- Some PDFs may use structures not yet covered by the current parser.
- Image-only PDFs still produce no useful text until OCR is added.
- Examples of unsupported or only partially supported PDF features include:
  - uncommon stream filters beyond the currently handled set
  - complex encrypted PDFs
  - incremental-update edge cases not represented in current samples
  - exotic font encodings or broken ToUnicode maps
  - uncommon annotation/action structures unrelated to invoice extraction
  - advanced XFA or interactive-form-driven document logic

## 2. Embedded XML Discovery

### Purpose

Identify embedded XML invoice payloads inside PDFs and expose them as candidate attachments for the XML path.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Internal\Pdf\PdfLowLevelReader.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\PdfAnalysisService.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\PdfEmbeddedFile.cs`

### How It Currently Works

- Scans parsed PDF objects for Filespec dictionaries and attachment-related structures.
- Reads file names from `/UF`, `/F`, and supported inline forms.
- Follows `/EF` references to embedded file streams.
- Supports attachments exposed either:
  - directly through Filespec objects
  - through FileAttachment annotations
  - through object streams after `/ObjStm` expansion

### Known Limitations / Edge Cases

- Discovery is based on common invoice-PDF structures, not the full PDF specification.
- Very unusual embedded-file arrangements may still be missed.
- Discovery only finds attachments; it does not itself validate XML usability.
- Concrete discovery edge cases include:
  - standalone `Filespec` objects without `FileAttachment` annotations
  - `FileAttachment` annotations whose Filespec is inline or indirect
  - attachment metadata stored inside `/ObjStm` object streams
  - PDFs containing multiple embedded files where only some are invoice XML
  - embedded attachments that are present but malformed, truncated, or not actually XML

## 3. XML Decoding and Parsing

### Purpose

Convert embedded XML attachment bytes into usable XML documents and hand them to invoice-number detection.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Services\XmlInvoiceDataExtractor.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\XmlInvoiceExtractionResult.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\XmlInvoiceDocument.cs`

### How It Currently Works

- Filters embedded files that look like XML by filename, subtype hint, or XML-like preview.
- Parses XML directly from raw attachment bytes using XML-aware parsing.
- This allows BOM and XML-declared encodings to be respected instead of assuming UTF-8.
- Returns only XML documents that successfully parse.

### Known Limitations / Edge Cases

- Only well-formed XML is accepted as usable.
- Non-XML invoice payloads are ignored.
- Schema-specific business extraction is still intentionally simple and delegated to detection.

## XML Encoding Handling

XML attachments must be parsed from raw bytes, not from a string pre-decoded as UTF-8.

Why this matters:

- Real invoice attachments may be UTF-8, UTF-16, ISO-8859-2, or another encoding declared in the XML.
- Some attachments include a BOM, and some rely on the XML declaration for encoding.
- If the byte stream is forced through UTF-8 first, the XML text can be corrupted before parsing begins.
- Corruption can make valid XML look malformed, even when the embedded file itself is correct.

Current approach:

- The XML extractor parses directly from the embedded file byte array.
- Encoding detection is left to XML-aware parsing, which is safer than assuming a fixed text encoding.
- This is especially important in mixed Hungarian invoice ecosystems where different issuers and generators use different XML encodings.

## 4. Text Extraction

### Purpose

Extract selectable text from PDFs when the XML path does not provide a usable invoice number.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Internal\Pdf\PdfLowLevelReader.cs`
- `C:\repo\ivren\src\Ivren.Core\Internal\Pdf\PdfTextTokenExtractor.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\PdfTextExtractionService.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\TextExtractionResult.cs`

### How It Currently Works

- Reads text operators from page content streams.
- Handles literal strings, hex strings, and `TJ` array text operators.
- Uses ToUnicode maps when available for better glyph decoding.
- Follows text inside Form XObjects referenced by page content streams.
- Produces a token list plus a combined `FullText` string for text-based detection.

### Known Limitations / Edge Cases

- Some PDFs still expose imperfect glyph decoding, especially around accented characters.
- Character spacing and fragmented words may require downstream normalization.
- Scanned/image-based PDFs still return no selectable text.

## 5. Text Normalization

### Purpose

Clean noisy extracted text into a form that is more stable for Hungarian invoice-number detection.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Services\PdfTextExtractionService.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\InvoiceNumberDetector.cs`

### How It Currently Works

- Replaces embedded control-character gaps with spaces.
- Collapses repeated whitespace.
- Repairs recurring fragmented Hungarian label forms.
- Collapses character-spaced text runs such as:
  - `S z á m l a s o r s z á m a`
  - into `Számla sorszáma`
- Produces a cleaner `FullText` for detector matching.

### Known Limitations / Edge Cases

- Normalization is rule-based, not linguistic or statistical.
- Some corrupted characters may still remain imperfect in extracted text.
- Supplier-specific hacks must be avoided unless they clearly generalize to a recurring document pattern.
- The normalization layer should stay focused on broad recurring patterns, not supplier-specific one-offs.
- Maintainability is a hard constraint: if normalization starts encoding issuer-by-issuer special cases, the fallback text path becomes fragile and difficult to reason about.

## 6. Invoice Number Detection

### Purpose

Determine the invoice number from XML first, then from text if XML does not succeed.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Services\InvoiceNumberDetector.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\InvoiceNumberDetectionResult.cs`

### How It Currently Works

- XML detection:
  - checks known element names such as `invoiceNumber` and `sorszam`
  - checks known attribute names such as `szlaszam`
  - falls back to broad XML content candidate matching if needed
- Text detection:
  - prefers label-linked candidates
  - uses Hungarian anchors such as `számlaszám`, `számla sorszáma`, and `számla száma`
  - requires invoice candidates to contain at least one digit
  - tries token-local matches, nearby-token matches, then full-text label-linked fallback

### Known Limitations / Edge Cases

- Detection is intentionally conservative and label-driven.
- Unlabeled numeric values are not accepted unless they survive the fallback rules.
- New supplier layouts may still require carefully justified detector improvements.

## 7. File Rename Decision

### Purpose

Make the final process decision after detection: sanitize the invoice number, compute the target filename, and rename or skip rename.

### Key Classes

- `C:\repo\ivren\src\Ivren.Core\Services\InvoiceFileProcessor.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\WindowsFilenameSanitizer.cs`
- `C:\repo\ivren\src\Ivren.Core\Services\FileRenameService.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\InvoiceFileProcessingResult.cs`
- `C:\repo\ivren\src\Ivren.Core\Models\InvoiceFileProcessingOptions.cs`

### How It Currently Works

- Runs the XML path first, then the text path if XML does not produce an invoice number.
- Sanitizes invalid Windows filename characters by replacing them with underscore.
- Preserves the original file extension from the source file.
- Supports dry-run mode, which reports the proposed target filename without renaming the original file.

### Known Limitations / Edge Cases

- Assumes one final invoice number per PDF.
- Rename conflict handling remains intentionally simple.
- OCR-based fallback is not yet present, so scanned files still fail after XML and text are exhausted.

## Failure Handling and Fallback Behavior

The pipeline is intentionally sequential and explicit about fallback behavior.

If embedded XML is missing:

- PDF analysis still completes.
- The XML extractor reports that no usable embedded XML invoice data was found.
- The processor falls back to selectable-text extraction.

If embedded XML is present but unusable:

- The attachment may still be discovered successfully.
- XML parsing can still fail if the attachment is malformed or not actually valid XML.
- In that case, the processor records the XML failure and falls back to text extraction.

If text extraction fails:

- The text extractor returns zero usable tokens.
- The text detection layer reports that no selectable text was extracted.
- This typically means the PDF is image-based, structurally unsupported for current text parsing, or otherwise unreadable through the current text path.

Final failure state:

- If XML detection does not produce an invoice number and text detection does not produce an invoice number, processing ends in a failed result.
- In the failed state:
  - no rename is performed
  - the result object reports failure
  - the log/messages explain whether the failure came from missing XML, unusable XML, missing text, or failed invoice-number detection
- OCR is the planned future fallback after XML and text, but it is not yet part of the active pipeline.

## Summary of Improvements Made During This Session

The current state reflects several focused improvements made while validating real Hungarian invoice PDFs:

- Added support for embedded-file discovery through:
  - Filespec objects
  - FileAttachment annotations
  - object streams (`/ObjStm`)
- Added traversal of Form XObjects for text extraction.
- Improved text normalization for fragmented Hungarian labels and character-spaced text.
- Expanded XML invoice detection to cover both XML elements and relevant XML attributes.
- Improved XML attachment decoding by parsing XML from raw bytes with encoding awareness.
- Cleaned attachment filename decoding for UTF-16-style PDF strings.
- Preserved the intended architecture:
  - business logic remains in `Ivren.Core`
  - `Ivren.WinForms` remains a thin UI

## Current Design Position

The current implementation is intentionally pragmatic:

- XML remains the preferred and highest-quality path.
- Text remains a robust fallback for selectable-text PDFs.
- OCR remains a future extension point for scanned/image-based invoices.

Structural parsing improvements should continue only when they are broadly applicable across real supplier PDFs. Encoding, normalization, and detection issues should continue to be separated and diagnosed independently before any new parser complexity is added.
