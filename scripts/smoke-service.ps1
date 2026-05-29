$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceProject = Join-Path $repoRoot "src\Ivren.Service\Ivren.Service.csproj"
$workDir = Join-Path $repoRoot "tmp\smoke-service"
$appDir = Join-Path $workDir "app"
$input01Dir = Join-Path $workDir "input\01"
$inputT1Dir = Join-Path $workDir "input\T1"
$renamed01Dir = Join-Path $workDir "renamed\01"
$renamedT1Dir = Join-Path $workDir "renamed\T1"
$failedDir = Join-Path $workDir "failed"
$logDir = Join-Path $workDir "logs"

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

function Remove-DirectorySafely {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $ExpectedPrefix
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolved = Resolve-Path -LiteralPath $Path
    if (-not $resolved.Path.StartsWith($ExpectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $($resolved.Path)"
    }

    Remove-Item -LiteralPath $resolved.Path -Recurse -Force
}

function Wait-Until {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Condition,
        [int] $TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

Write-Host "== Ivren Service smoke test =="
Write-Host "Repository: $repoRoot"

Remove-DirectorySafely -Path $workDir -ExpectedPrefix $workDir
New-Item -ItemType Directory -Path $appDir, $input01Dir, $inputT1Dir, $renamed01Dir, $renamedT1Dir, $failedDir, $logDir | Out-Null

Write-Host "Publishing Service project to an isolated smoke-test folder..."
Invoke-Checked { dotnet publish $serviceProject -c Debug -o $appDir -v minimal } "dotnet publish Ivren.Service"

$sampleSource = Join-Path $repoRoot "mintaszamla\ACE Telecom --20260009884_e_1.pdf"
if (-not (Test-Path -LiteralPath $sampleSource)) {
    throw "Required smoke sample is missing: $sampleSource"
}

Copy-Item -LiteralPath $sampleSource -Destination (Join-Path $input01Dir "ace-smoke.pdf")
Set-Content -LiteralPath (Join-Path $inputT1Dir "invalid-smoke.pdf") -Value "This is intentionally not a valid PDF." -Encoding ASCII

$settings = [ordered]@{
    FolderPairs = @(
        [ordered]@{
            CompanyCode = "01"
            InputFolder = $input01Dir
            RenamedFolder = $renamed01Dir
        },
        [ordered]@{
            CompanyCode = "T1"
            InputFolder = $inputT1Dir
            RenamedFolder = $renamedT1Dir
        }
    )
    FailedFolder = $failedDir
    AuditLogFolder = $logDir
    PollIntervalSeconds = 30
    FileReadyDelaySeconds = 0
    DryRun = $false
}

$settingsPath = Join-Path $appDir "Ivren.Service.settings.json"
$settings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$exePath = Join-Path $appDir "Ivren.Service.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Service executable was not published: $exePath"
}

$stdoutPath = Join-Path $workDir "service.stdout.log"
$stderrPath = Join-Path $workDir "service.stderr.log"

Write-Host "Starting Service executable interactively..."
$process = Start-Process -FilePath $exePath `
    -WorkingDirectory $appDir `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -WindowStyle Hidden `
    -PassThru

try {
    $renamedTarget = Join-Path $renamed01Dir "2026~9884.pdf"
    $failedTarget = Join-Path $failedDir "invalid-smoke.pdf"

    $processed = Wait-Until -TimeoutSeconds 45 -Condition {
        (Test-Path -LiteralPath $renamedTarget) -and (Test-Path -LiteralPath $failedTarget)
    }

    if (-not $processed) {
        throw "Service did not process both smoke files within the timeout."
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $null = $process.WaitForExit(5000)
    }
}

$auditLog = Get-ChildItem -LiteralPath $logDir -Filter "ivren-audit-*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
$serviceLog = Get-ChildItem -LiteralPath $logDir -Filter "ivren-service-*.log" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $auditLog) {
    throw "Audit log was not written in $logDir."
}

if (-not $serviceLog) {
    throw "Service log was not written in $logDir."
}

$auditText = Get-Content -LiteralPath $auditLog.FullName -Raw
if ($auditText -notmatch '"outcome"\s*:\s*"Renamed"') {
    throw "Audit log does not contain a Renamed outcome."
}

if ($auditText -notmatch '"outcome"\s*:\s*"Failed"') {
    throw "Audit log does not contain a Failed outcome."
}

$invalidSettings = [ordered]@{
    FailedFolder = $failedDir
    AuditLogFolder = $logDir
    DryRun = $false
}
$invalidSettings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$invalidStdoutPath = Join-Path $workDir "invalid-settings.stdout.log"
$invalidStderrPath = Join-Path $workDir "invalid-settings.stderr.log"
$invalidProcess = Start-Process -FilePath $exePath `
    -WorkingDirectory $appDir `
    -RedirectStandardOutput $invalidStdoutPath `
    -RedirectStandardError $invalidStderrPath `
    -WindowStyle Hidden `
    -PassThru

try {
    $startupFailed = Wait-Until -TimeoutSeconds 10 -Condition {
        $invalidProcess.HasExited
    }

    if (-not $startupFailed) {
        throw "Service did not fail fast for invalid settings within the timeout."
    }

    $invalidProcess.Refresh()
    if ($invalidProcess.ExitCode -eq 0) {
        throw "Service exited successfully even though settings were invalid."
    }
}
finally {
    if ($invalidProcess -and -not $invalidProcess.HasExited) {
        Stop-Process -Id $invalidProcess.Id -Force
        $null = $invalidProcess.WaitForExit(5000)
    }
}

$startupErrorLog = Get-ChildItem -LiteralPath $appDir -Filter "ivren-startup-error-*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $startupErrorLog) {
    throw "Startup error log was not written for invalid settings."
}

$invalidStderrText = Get-Content -LiteralPath $invalidStderrPath -Raw
if ($invalidStderrText -notmatch "Settings file is invalid") {
    throw "Invalid settings stderr did not contain the expected validation error."
}

Write-Host "Renamed output: $renamedTarget"
Write-Host "Failed output:  $failedTarget"
Write-Host "Audit log:      $($auditLog.FullName)"
Write-Host "Service log:    $($serviceLog.FullName)"
Write-Host "Startup error:  $($startupErrorLog.FullName)"
Write-Host "Service smoke test passed."
