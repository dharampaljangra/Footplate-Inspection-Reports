<#
  Builds, tests and signs the Windows (x64) version.
  Requirements: .NET 10 SDK with the MAUI workload (dotnet workload install maui), Windows SDK (signtool).
  Usage:  .\build\build-windows.ps1 -PfxPath artifacts\Footplate-signing.pfx -PfxPassword "..."
#>
param([string]$PfxPath = "", [string]$PfxPassword = "")
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
Push-Location $root
try {
  dotnet test tests\FootplateIR.Core.Tests\FootplateIR.Core.Tests.csproj -c Release
  if ($LASTEXITCODE -ne 0) { throw "Unit tests failed - build stopped." }
  $out = "$root\artifacts\windows"
  dotnet publish src\FootplateIR.App\FootplateIR.App.csproj -c Release -f net10.0-windows10.0.19041.0 -o $out
  if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
  if ($PfxPath) { & "$PSScriptRoot\sign-windows.ps1" -Folder $out -PfxPath $PfxPath -PfxPassword $PfxPassword }
  Compress-Archive -Path "$out\*" -DestinationPath "$root\artifacts\Footplate-Reports-Windows-x64.zip" -Force
  Get-FileHash "$root\artifacts\Footplate-Reports-Windows-x64.zip" -Algorithm SHA256 | Format-List
  Write-Host "Done: artifacts\Footplate-Reports-Windows-x64.zip  (run FootplateIR.App.exe)"
} finally { Pop-Location }
