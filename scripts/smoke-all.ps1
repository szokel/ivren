$ErrorActionPreference = "Stop"

$scriptRoot = $PSScriptRoot

& (Join-Path $scriptRoot "smoke-core.ps1")
& (Join-Path $scriptRoot "smoke-service.ps1")
& (Join-Path $scriptRoot "smoke-winforms.ps1")

Write-Host "All Ivren smoke tests passed."
