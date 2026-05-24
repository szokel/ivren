$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $repoRoot "src\Ivren.Core\Ivren.Core.csproj"
$runnerDir = Join-Path $repoRoot "tmp\smoke-core-runner"
$outputDir = Join-Path $repoRoot "tmp\smoke-core-output"
$supplierProfilesPath = Join-Path $repoRoot "src\Ivren.WinForms\Ivren.SupplierProfiles.json"

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

Write-Host "== Ivren Core smoke test =="
Write-Host "Repository: $repoRoot"
Write-Host "Building Core project..."
Invoke-Checked { dotnet build $coreProject -v minimal } "dotnet build Ivren.Core"

Remove-DirectorySafely -Path $runnerDir -ExpectedPrefix $runnerDir
Remove-DirectorySafely -Path $outputDir -ExpectedPrefix $outputDir
New-Item -ItemType Directory -Path $runnerDir | Out-Null
New-Item -ItemType Directory -Path $outputDir | Out-Null

$runnerProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$coreProject" />
  </ItemGroup>
</Project>
"@

$runnerProgram = @'
using Ivren.Core.Contracts;
using Ivren.Core.Models;
using Ivren.Core.Services;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: SmokeCoreRunner <repoRoot> <outputDir> <supplierProfilesPath>");
    return 2;
}

var repoRoot = args[0];
var outputDir = args[1];
var supplierProfilesPath = args[2];
var sampleDir = Path.Combine(repoRoot, "mintaszamla");
var renamedDir = Path.Combine(outputDir, "renamed");
var failedDir = Path.Combine(outputDir, "failed");
var logDir = Path.Combine(outputDir, "logs");

Directory.CreateDirectory(renamedDir);
Directory.CreateDirectory(failedDir);
Directory.CreateDirectory(logDir);

IInvoiceFileProcessor processor = new InvoiceFileProcessor(
    new PdfAnalysisService(),
    new XmlInvoiceDataExtractor(),
    new PdfTextExtractionService(),
    new InvoiceNumberDetector(),
    new WindowsFilenameSanitizer(),
    new FileRenameService(),
    new JsonLinesAuditLogService(),
    new JsonSupplierProfileProvider(supplierProfilesPath));

var options = new InvoiceFileProcessingOptions(
    DryRun: true,
    RenamedFolderPath: renamedDir,
    FailedFolderPath: failedDir,
    AuditLogFolderPath: logDir);

var cases = new[]
{
    new SmokeCase(
        "ACE XML invoice",
        "ACE Telecom --20260009884_e_1.pdf",
        "2026/9884",
        DetectionSource.Xml,
        "2026~9884.pdf"),
    new SmokeCase(
        "Yettel XML invoice",
        "Yettel --100349122228.pdf",
        "100349122228",
        DetectionSource.Xml,
        "100349122228.pdf"),
    new SmokeCase(
        "NOPA direct-label text invoice",
        "NOPA --16124427.pdf",
        "16124427",
        DetectionSource.Text,
        "16124427.pdf"),
    new SmokeCase(
        "VANNET standalone heading text invoice",
        "VANNET --VT-SAT-2025-5290.pdf",
        "VT-SAT-2025-5290",
        DetectionSource.Text,
        "VT-SAT-2025-5290.pdf")
};

var failures = new List<string>();

foreach (var smokeCase in cases)
{
    var sourcePath = Path.Combine(sampleDir, smokeCase.FileName);
    if (!File.Exists(sourcePath))
    {
        failures.Add($"{smokeCase.Name}: sample file is missing: {sourcePath}");
        continue;
    }

    var result = processor.Process(sourcePath, options);
    var actualTargetFileName = string.IsNullOrWhiteSpace(result.TargetFilePath)
        ? string.Empty
        : Path.GetFileName(result.TargetFilePath);

    Console.WriteLine($"{smokeCase.Name}: Status={result.Status}; Source={result.DetectionSource}; Invoice={result.InvoiceNumber}; Target={actualTargetFileName}; Confidence={result.ConfidenceScore:0.00} {result.ConfidenceLevel}; DryRun={result.DryRunEnabled}");

    if (result.Status != FileProcessStatus.Success)
    {
        failures.Add($"{smokeCase.Name}: expected Status=Success, got {result.Status}. {result.Summary}");
    }

    if (result.DetectionSource != smokeCase.ExpectedSource)
    {
        failures.Add($"{smokeCase.Name}: expected Source={smokeCase.ExpectedSource}, got {result.DetectionSource}.");
    }

    if (!string.Equals(result.InvoiceNumber, smokeCase.ExpectedInvoiceNumber, StringComparison.Ordinal))
    {
        failures.Add($"{smokeCase.Name}: expected Invoice={smokeCase.ExpectedInvoiceNumber}, got {result.InvoiceNumber ?? "(null)"}.");
    }

    if (!string.Equals(actualTargetFileName, smokeCase.ExpectedTargetFileName, StringComparison.OrdinalIgnoreCase))
    {
        failures.Add($"{smokeCase.Name}: expected Target={smokeCase.ExpectedTargetFileName}, got {actualTargetFileName}.");
    }

    if (!result.RenameSkippedDueToDryRun || result.Renamed)
    {
        failures.Add($"{smokeCase.Name}: dry-run should skip move/rename.");
    }

    if (!File.Exists(sourcePath))
    {
        failures.Add($"{smokeCase.Name}: source file disappeared during dry-run.");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Core smoke test failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(" - " + failure);
    }

    return 1;
}

Console.WriteLine("Core smoke test passed.");
return 0;

internal sealed record SmokeCase(
    string Name,
    string FileName,
    string ExpectedInvoiceNumber,
    DetectionSource ExpectedSource,
    string ExpectedTargetFileName);
'@

Set-Content -LiteralPath (Join-Path $runnerDir "SmokeCoreRunner.csproj") -Value $runnerProject -Encoding UTF8
Set-Content -LiteralPath (Join-Path $runnerDir "Program.cs") -Value $runnerProgram -Encoding UTF8

Write-Host "Running Core pipeline validation in dry-run mode..."
Invoke-Checked { dotnet run --project (Join-Path $runnerDir "SmokeCoreRunner.csproj") -- $repoRoot $outputDir $supplierProfilesPath } "dotnet run Core smoke runner"

Write-Host "Core smoke test completed successfully."
