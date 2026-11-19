<# Authenticode-signs FootplateIR.App.exe and FootplateIR.Core.dll with SHA-256 and an RFC 3161 timestamp. #>
param([Parameter(Mandatory)][string]$Folder, [Parameter(Mandatory)][string]$PfxPath, [Parameter(Mandatory)][string]$PfxPassword,
      [string]$TimestampUrl = "http://timestamp.digicert.com")
$ErrorActionPreference = "Stop"
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
            Where-Object { $_.FullName -match "x64" } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw "signtool.exe not found - install the Windows 10/11 SDK." }
$files = @("$Folder\FootplateIR.App.exe", "$Folder\FootplateIR.App.dll", "$Folder\FootplateIR.Core.dll") | Where-Object { Test-Path $_ }
& $signtool.FullName sign /fd SHA256 /f $PfxPath /p $PfxPassword /tr $TimestampUrl /td SHA256 /d "Footplate Inspection Reports" $files
if ($LASTEXITCODE -ne 0) { throw "Signing failed." }
& $signtool.FullName verify /pa /v "$Folder\FootplateIR.App.exe"
