# Ivren Local Service Test Environment

This document describes the local developer-machine environment for testing `Ivren.Service` under a dedicated Windows account.

## Purpose

The goal is to test the service in a setup that is close to production:

- A dedicated Windows user runs the service.
- PDFs are manually dropped into an input folder.
- Successfully processed PDFs are moved to a renamed folder.
- Failed PDFs are moved to a failed folder.
- Service logs and audit logs are written to a logs folder.

## Default Local Test Layout

```text
D:\DATA\ivren-test\input
D:\DATA\ivren-test\renamed
D:\DATA\ivren-test\failed
D:\DATA\ivren-test\logs
```

The service executable is published to:

```text
C:\var\ivren-test\service
```

The local Windows service is named:

```text
IvrenServiceTest
```

The local service account is:

```text
.\ivren
```

## Setup

Open PowerShell as Administrator and run from `C:\repo\ivren`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-service-test.ps1
```

The script will:

- Create the local `.\ivren` user if it does not already exist.
- Prompt for the service account password.
- Create the test folders.
- Publish `Ivren.Service`.
- Write `Ivren.Service.settings.json` beside the service executable.
- Grant the service account access to the data and service folders.
- Create and start `IvrenServiceTest`.

If `IvrenServiceTest` already exists and you want to recreate it:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-local-service-test.ps1 -RecreateService
```

## Manual Test Flow

1. Start the service if it is not already running:

```powershell
Start-Service IvrenServiceTest
```

2. Drop one or more PDF files into:

```text
D:\DATA\ivren-test\input
```

3. Wait a few seconds.

4. Check successful files:

```text
D:\DATA\ivren-test\renamed
```

5. Check failed files:

```text
D:\DATA\ivren-test\failed
```

6. Check logs:

```powershell
Get-Content "D:\DATA\ivren-test\logs\ivren-service-$(Get-Date -Format yyyy-MM-dd).log" -Tail 80
Get-Content "D:\DATA\ivren-test\logs\ivren-audit-$(Get-Date -Format yyyy-MM-dd).log" -Tail 20
```

## Useful Commands

```powershell
Get-Service IvrenServiceTest
Stop-Service IvrenServiceTest
Start-Service IvrenServiceTest
Restart-Service IvrenServiceTest
```

## Notes

- The setup script creates a local test service only. It does not modify the production-style `IvrenService` name documented for WOVER.
- The default setup uses `DryRun: false`, so files dropped into the input folder are really moved.
- Use a copy of any important PDF when testing manually.
- To test dry-run behavior, run the setup script with `-DryRun -RecreateService`, or edit `Ivren.Service.settings.json` and restart the service.
