@echo off
cd /d C:\repo\ivren

dotnet publish .\src\Ivren.WinForms\Ivren.WinForms.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o C:\repo\ivren\publish\Ivren.WinForms-win-x64
