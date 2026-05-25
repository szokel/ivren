param(
    [string] $ServiceName = "IvrenServiceTest",
    [string] $UserName = "ivren",
    [string] $DataRoot = "D:\DATA\ivren-test",
    [string] $ServiceRoot = "C:\var\ivren-test\service",
    [int] $PollIntervalSeconds = 5,
    [int] $FileReadyDelaySeconds = 2,
    [switch] $DryRun,
    [switch] $RecreateService,
    [switch] $NoStart
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$serviceProject = Join-Path $repoRoot "src\Ivren.Service\Ivren.Service.csproj"
$serviceExe = Join-Path $ServiceRoot "Ivren.Service.exe"
$inputFolder = Join-Path $DataRoot "input"
$renamedFolder = Join-Path $DataRoot "renamed"
$failedFolder = Join-Path $DataRoot "failed"
$logsFolder = Join-Path $DataRoot "logs"
$displayName = "Ivren PDF Invoice Rename Service (Local Test)"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isAdmin) {
        throw "This script must be run from an elevated PowerShell window. Please start PowerShell as Administrator and run it again."
    }
}

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

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
        Write-Host "Created folder: $Path"
    }
}

function Ensure-LocalServiceUser {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $existingUser = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue

    if ($existingUser) {
        Write-Host "Local user already exists: .\$Name"
        Write-Host "Enter the existing password for .\$Name so the Windows service can be configured."
        return Read-Host "Password for .\$Name" -AsSecureString
    }

    Write-Host "Creating local user: .\$Name"
    $password = Read-Host "New password for .\$Name" -AsSecureString
    New-LocalUser `
        -Name $Name `
        -Password $password `
        -PasswordNeverExpires `
        -UserMayNotChangePassword `
        -Description "Ivren local service test account" | Out-Null

    try {
        Add-LocalGroupMember -Group "Users" -Member $Name -ErrorAction Stop
    }
    catch {
        Write-Warning "Could not add .\$Name to local Users group. It may already be a member. Details: $($_.Exception.Message)"
    }

    return $password
}

function Remove-ServiceIfRequested {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) {
        return
    }

    if (-not $RecreateService) {
        throw "Service '$Name' already exists. Re-run with -RecreateService if you want this script to stop, delete, and recreate it."
    }

    Write-Host "Stopping existing service: $Name"
    if ($service.Status -ne "Stopped") {
        Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
        $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(20))
    }

    Write-Host "Deleting existing service: $Name"
    Invoke-Checked { sc.exe delete $Name } "sc.exe delete $Name"

    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Service -Name $Name -ErrorAction SilentlyContinue)) {
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for service '$Name' to be deleted."
}

function Grant-FolderPermission {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Account,
        [Parameter(Mandatory = $true)]
        [string] $Permission
    )

    Invoke-Checked { icacls $Path /grant "${Account}:$Permission" /T } "icacls $Path /grant ${Account}:$Permission"
}

Assert-Administrator

Write-Host "== Ivren local service test setup =="
Write-Host "Repository:        $repoRoot"
Write-Host "Service name:      $ServiceName"
Write-Host "Service account:   .\$UserName"
Write-Host "Data root:         $DataRoot"
Write-Host "Service root:      $ServiceRoot"
Write-Host "DryRun:            $($DryRun.IsPresent)"

$password = Ensure-LocalServiceUser -Name $UserName

Ensure-Directory -Path $DataRoot
Ensure-Directory -Path $inputFolder
Ensure-Directory -Path $renamedFolder
Ensure-Directory -Path $failedFolder
Ensure-Directory -Path $logsFolder
Ensure-Directory -Path $ServiceRoot

Write-Host "Publishing Ivren.Service..."
Invoke-Checked { dotnet publish $serviceProject -c Release -o $ServiceRoot -v minimal } "dotnet publish Ivren.Service"

$settings = [ordered]@{
    InputFolder = $inputFolder
    RenamedFolder = $renamedFolder
    FailedFolder = $failedFolder
    AuditLogFolder = $logsFolder
    PollIntervalSeconds = $PollIntervalSeconds
    FileReadyDelaySeconds = $FileReadyDelaySeconds
    DryRun = [bool]$DryRun
}

$settingsPath = Join-Path $ServiceRoot "Ivren.Service.settings.json"
$settings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
Write-Host "Wrote settings: $settingsPath"

$accountName = ".\$UserName"
Grant-FolderPermission -Path $DataRoot -Account $accountName -Permission "(OI)(CI)M"
Grant-FolderPermission -Path $ServiceRoot -Account $accountName -Permission "(OI)(CI)RX"

Remove-ServiceIfRequested -Name $ServiceName

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    $credential = [pscredential]::new($accountName, $password)

    Write-Host "Creating Windows service: $ServiceName"
    New-Service `
        -Name $ServiceName `
        -BinaryPathName "`"$serviceExe`"" `
        -DisplayName $displayName `
        -Description "Local test instance of Ivren PDF invoice rename service." `
        -StartupType Manual `
        -Credential $credential | Out-Null
}

if (-not $NoStart) {
    Write-Host "Starting service: $ServiceName"
    Start-Service -Name $ServiceName
}

Write-Host ""
Write-Host "Setup completed."
Write-Host "Drop test PDFs here:       $inputFolder"
Write-Host "Renamed PDFs appear here:  $renamedFolder"
Write-Host "Failed PDFs appear here:   $failedFolder"
Write-Host "Logs are written here:     $logsFolder"
Write-Host ""
Write-Host "Useful commands:"
Write-Host "  Get-Service $ServiceName"
Write-Host "  Stop-Service $ServiceName"
Write-Host "  Start-Service $ServiceName"
Write-Host "  Get-Content `"$logsFolder\ivren-service-$(Get-Date -Format yyyy-MM-dd).log`" -Tail 80"
Write-Host "  Get-Content `"$logsFolder\ivren-audit-$(Get-Date -Format yyyy-MM-dd).log`" -Tail 20"
