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

Create these folders on the server data drive:

```text
D:\DATA\ivren\input
D:\DATA\ivren\renamed
D:\DATA\ivren\failed
D:\DATA\ivren\logs
```

The service skips processing and logs a clear error if a configured folder is missing.

## Settings

`Ivren.Service.settings.json` lives beside the executable:

```json
{
  "InputFolder": "D:\\DATA\\ivren\\input",
  "RenamedFolder": "D:\\DATA\\ivren\\renamed",
  "FailedFolder": "D:\\DATA\\ivren\\failed",
  "AuditLogFolder": "D:\\DATA\\ivren\\logs",
  "PollIntervalSeconds": 30,
  "FileReadyDelaySeconds": 10,
  "DryRun": false
}
```

Use `DryRun: true` for smoke testing. In dry-run mode, PDFs are analyzed and target paths are logged, but files are not moved or renamed and audit logs are not written by the Core audit workflow.

## Service Account

The recommended account is:

```text
MEGA\svc_ivren
```

Grant this account read/write/modify access to:

```text
D:\DATA\ivren
```

It needs access to the input folder, renamed folder, failed folder, and logs folder.

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
D:\DATA\ivren\logs\ivren-service-YYYY-MM-DD.log
```

Audit logs remain separate and are written by `Ivren.Core`:

```text
D:\DATA\ivren\logs\ivren-audit-YYYY-MM-DD.log
```

Service logs include startup configuration, effective Windows user, app base directory, supplier profile path, processing cycles, per-file processing, and exceptions.

## Troubleshooting

- If no PDFs are processed, check that `InputFolder` exists and contains `*.pdf` files directly in that folder.
- If files are skipped as not ready, confirm they are no longer being copied and wait longer than `FileReadyDelaySeconds`.
- If all cycles are skipped, check the service log for missing folder errors.
- If XML profiles seem stale, confirm the deployed `Ivren.SupplierProfiles.json` is beside the executable.
- If the service cannot move files, verify `MEGA\svc_ivren` has modify permission on `D:\DATA\ivren`.
- If testing safely, set `DryRun` to `true` and restart the service.
