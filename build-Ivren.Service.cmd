@echo off
setlocal

cd /d C:\repo\ivren

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd"') do set BUILD_DATE=%%i

set OUT=C:\repo\ivren\publish\Ivren.Service-win-x64-%BUILD_DATE%
set ZIP=C:\repo\ivren\publish\Ivren.Service-win-x64-%BUILD_DATE%.zip

echo Publishing Ivren.Service to %OUT%

dotnet publish .\src\Ivren.Service\Ivren.Service.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "%OUT%"

if errorlevel 1 exit /b %errorlevel%

echo Creating ZIP %ZIP%
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path -LiteralPath '%ZIP%') { Remove-Item -LiteralPath '%ZIP%' -Force }; Compress-Archive -Path (Join-Path '%OUT%' '*') -DestinationPath '%ZIP%' -CompressionLevel Optimal"

if errorlevel 1 exit /b %errorlevel%

echo.
echo Service build completed:
echo   Folder: %OUT%
echo   ZIP:    %ZIP%
