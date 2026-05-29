# Ivren Service Deployment

## Purpose

`Ivren.Service` is the non-interactive Windows Service version of Ivren. It reuses `Ivren.Core` for PDF analysis, XML-first invoice detection, text fallback, password-protected PDF handling, confidence scoring, file move/rename behavior, conflict-safe target filenames, and audit logging.

`Ivren.WinForms` remains available for interactive testing and manual diagnostics.

## Publish

Run from `C:\repo\ivren`:

```powershell
dotnet publish .\src\Ivren.Service\Ivren.Service.csproj -c Release -r win-x64 --self-contained true -o C:\repo\ivren\publish\Ivren.Service-win-x64
```

Suggested deployment folder on WOVER:

```text
C:\var\ivren\service
```

Copy the published folder contents into that folder. Keep these files beside `Ivren.Service.exe`:

- `Ivren.Service.settings.json`
- `Ivren.SupplierProfiles.json`

## Data Folders

Create one input and one renamed/output folder pair for each company code. For example:

```text
C:\SzallitoiSzamlak_Atnevezeshez\01
C:\SzallitoiSzamlak_Atnevezeshez\10
C:\SzallitoiSzamlak_Atnevezeshez\18
C:\SzallitoiSzamlak_Atnevezeshez\23
C:\SzallitoiSzamlak_Atnevezeshez\27
C:\SzallitoiSzamlak_Atnevezeshez\T1

\\scala\SzallitoiSzamlak\01
\\scala\SzallitoiSzamlak\10
\\scala\SzallitoiSzamlak\18
\\scala\SzallitoiSzamlak\23
\\scala\SzallitoiSzamlak\27
\\scala\SzallitoiSzamlak\T1
```

Create one shared failed folder and one shared audit/log folder:

```text
C:\SzallitoiSzamlak_Atnevezeshez\Program\szamlak\nem_atnevezheto_szamlak
C:\SzallitoiSzamlak_Atnevezeshez\Program\szamlak\audit_log
```

The service skips processing for a missing company folder pair and logs a clear error. The service creates the audit/log folder if it is missing and permissions allow it. If the shared failed folder is missing, the whole processing cycle is skipped.

## Settings

`Ivren.Service.settings.json` lives beside the executable:

```json
{
  "FolderPairs": [
    {
      "CompanyCode": "01",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\01",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\01"
    },
    {
      "CompanyCode": "10",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\10",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\10"
    },
    {
      "CompanyCode": "18",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\18",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\18"
    },
    {
      "CompanyCode": "23",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\23",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\23"
    },
    {
      "CompanyCode": "27",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\27",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\27"
    },
    {
      "CompanyCode": "T1",
      "InputFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\T1",
      "RenamedFolder": "\\\\scala\\SzallitoiSzamlak\\T1"
    }
  ],
  "FailedFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\Program\\szamlak\\nem_atnevezheto_szamlak",
  "AuditLogFolder": "C:\\SzallitoiSzamlak_Atnevezeshez\\Program\\szamlak\\audit_log",
  "PollIntervalSeconds": 10,
  "FileReadyDelaySeconds": 5,
  "DryRun": false
}
```

Windows paths in JSON must use doubled backslashes. For example, write `C:\\folder\\subfolder` and `\\\\server\\share\\folder`.

Use `DryRun: true` for smoke testing. In dry-run mode, PDFs are analyzed and target paths are logged, but files are not moved or renamed and audit logs are not written by the Core audit workflow.

If the settings file is missing, malformed, or structurally invalid, the service does not start with built-in defaults. It writes a startup failure to stderr and, if possible, to `ivren-startup-error-YYYY-MM-DD.log` beside the executable.

## Service Account

The recommended account is:

```text
MEGA\svc_ivren
```

Grant this account read/write/modify access to:

```text
C:\SzallitoiSzamlak_Atnevezeshez
\\scala\SzallitoiSzamlak
```

It needs access to all input folders, all renamed/output folders, the shared failed folder, and the audit/log folder.

## Install Windows Service

Run from an elevated command prompt on WOVER:

```cmd
sc.exe create IvrenService binPath= "C:\var\ivren\service\Ivren.Service.exe" start= auto obj= "MEGA\svc_ivren" DisplayName= "Ivren PDF Invoice Rename Service"
```

Set the service account password when prompted by your normal server administration process if needed.

## Start And Stop

```cmd
sc.exe start IvrenService
sc.exe stop IvrenService
sc.exe query IvrenService
```

For interactive troubleshooting, run:

```powershell
C:\var\ivren\service\Ivren.Service.exe
```

Console logging is enabled when run interactively.

## Logs

Service logs are written daily:

```text
C:\SzallitoiSzamlak_Atnevezeshez\Program\szamlak\audit_log\ivren-service-YYYY-MM-DD.log
```

Audit logs remain separate and are written by `Ivren.Core`:

```text
C:\SzallitoiSzamlak_Atnevezeshez\Program\szamlak\audit_log\ivren-audit-YYYY-MM-DD.log
```

Service logs include startup configuration, effective Windows user, app base directory, supplier profile path, processing cycles, per-file processing, and exceptions.

## Troubleshooting

- If the service does not start, check `Ivren.Service.settings.json` for valid JSON. Windows paths must use doubled backslashes.
- If no PDFs are processed, check that each configured `FolderPairs[].InputFolder` exists and contains `*.pdf` files directly in that folder.
- If files are skipped as not ready, confirm they are no longer being copied and wait longer than `FileReadyDelaySeconds`.
- If a company folder is skipped, check the service log for that `CompanyCode`.
- If all cycles are skipped, check the service log for missing shared failed folder errors.
- If XML profiles seem stale, confirm the deployed `Ivren.SupplierProfiles.json` is beside the executable.
- If the service cannot move files, verify `MEGA\svc_ivren` has modify permission on the relevant input/output/failed/audit folders.
- If testing safely, set `DryRun` to `true` and restart the service.
