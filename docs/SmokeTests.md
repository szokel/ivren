# Ivren Smoke Tests

This repository includes lightweight command-line smoke tests for the three application areas:

- `Ivren.Core`
- `Ivren.Service`
- `Ivren.WinForms`

The scripts are intentionally simple. They do not introduce a test framework, do not change production code, and do not rename or move original sample PDF files.

## Run All Smoke Tests

From `C:\repo\ivren`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-all.ps1
```

## Run Individual Smoke Tests

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-core.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-service.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-winforms.ps1
```

## Core Smoke Test

`scripts\smoke-core.ps1` builds `Ivren.Core`, creates a temporary runner under `tmp\smoke-core-runner`, and runs the real Core pipeline in dry-run mode against bundled sample PDFs.

It validates:

- XML-based detection with the ACE sample.
- Text-based detection with the Yettel sample.
- Text-based standalone-heading detection with the VANNET sample.
- Dry-run does not rename or move original sample files.
- Slash-containing invoice numbers use tilde-based filename sanitization.

Temporary output is written under:

```text
tmp\smoke-core-output
```

## Service Smoke Test

`scripts\smoke-service.ps1` publishes `Ivren.Service` to an isolated temporary folder, writes a smoke-test settings file beside the published executable, starts the service executable interactively, and lets it process copied files from a temporary input folder.

It validates:

- The service starts from a published folder.
- Settings are loaded from the executable folder.
- Multiple company-specific input/output folder pairs are supported.
- A valid invoice PDF is moved to the renamed folder.
- An invalid PDF is moved to the failed folder.
- Audit and service logs are written.
- Structurally invalid service settings fail fast and write a startup error log instead of falling back to built-in folders.

Temporary output is written under:

```text
tmp\smoke-service
```

The original sample PDFs are copied before processing and are not modified.

## WinForms Smoke Test

`scripts\smoke-winforms.ps1` builds `Ivren.WinForms` and verifies that the expected executable-side files are present in the build output.

It validates:

- `Ivren.WinForms.exe` is produced.
- `Ivren.Core.dll` is copied beside the executable.
- `Ivren.WinForms.settings.json` is copied beside the executable and is valid JSON.
- `Ivren.SupplierProfiles.json` is copied beside the executable and is valid JSON.

The script does not launch the GUI. This keeps the smoke test safe for command-line and audit runs.
