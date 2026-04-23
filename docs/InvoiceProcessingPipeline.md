# Invoice Processing Pipeline

## Purpose

This document summarizes the current invoice-processing pipeline in the `ivren` solution. It describes the processing layers, the main classes involved, the current detection priority, the PDF structures supported so far, and the main improvements made during the current implementation session.

The application is designed with these principles:

- `Ivren.Core` contains all business logic.
- `Ivren.WinForms` is a thin operator and test UI.
- Embedded XML is the primary extraction path.
- Text extraction is the secondary fallback path.
- OCR is planned for later and is not yet implemented.

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

- It is intentionally limited and does not aim to cover all PDF features.
- Unsupported compression/filter combinations may still block extraction.
- Some PDFs may use structures not yet covered by the current parser.
- Image-only PDFs still produce no useful text until OCR is added.

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
- The normalization layer should stay focused on broad recurring patterns, not supplier-specific one-offs.

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
