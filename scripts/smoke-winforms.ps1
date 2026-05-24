$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$winFormsProject = Join-Path $repoRoot "src\Ivren.WinForms\Ivren.WinForms.csproj"
$outputDir = Join-Path $repoRoot "src\Ivren.WinForms\bin\Debug\net8.0-windows"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Command,
        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Description"
    }
}

Write-Host "== Ivren WinForms smoke test =="
Write-Host "Repository: $repoRoot"
Write-Host "Building WinForms project..."
Invoke-Checked { dotnet build $winFormsProject -v minimal } "dotnet build Ivren.WinForms"

$requiredFiles = @(
    "Ivren.WinForms.exe",
    "Ivren.WinForms.dll",
    "Ivren.Core.dll",
    "Ivren.WinForms.settings.json",
    "Ivren.SupplierProfiles.json"
)

$failures = New-Object System.Collections.Generic.List[string]
foreach ($fileName in $requiredFiles) {
    $path = Join-Path $outputDir $fileName
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Missing output file: $path")
    }
}

$settingsPath = Join-Path $outputDir "Ivren.WinForms.settings.json"
if (Test-Path -LiteralPath $settingsPath) {
    try {
        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        foreach ($property in @("DefaultFolder", "RenamedFolder", "FailedFolder", "AuditLogFolder")) {
            if (-not $settings.PSObject.Properties.Name.Contains($property)) {
                $failures.Add("Settings file is missing property: $property")
            }
        }
    }
    catch {
        $failures.Add("Settings file is not valid JSON: $($_.Exception.Message)")
    }
}

$profilesPath = Join-Path $outputDir "Ivren.SupplierProfiles.json"
if (Test-Path -LiteralPath $profilesPath) {
    try {
        $profiles = Get-Content -LiteralPath $profilesPath -Raw | ConvertFrom-Json
        if (-not $profiles.Default) {
            $failures.Add("Supplier profiles file is missing Default profile.")
        }
    }
    catch {
        $failures.Add("Supplier profiles file is not valid JSON: $($_.Exception.Message)")
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("WinForms smoke test failed:`n - " + ($failures -join "`n - "))
    exit 1
}

Write-Host "WinForms output folder: $outputDir"
Write-Host "WinForms smoke test passed."
