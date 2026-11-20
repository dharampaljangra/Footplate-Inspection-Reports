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
# A self-signed publisher is not trusted on the build machine, so check that every file carries a signature
foreach ($f in $files) {
  $sig = Get-AuthenticodeSignature $f
  if ($sig.Status -eq "NotSigned" -or -not $sig.SignerCertificate) { throw "$f is not signed." }
  Write-Host "$(Split-Path $f -Leaf): signed by $($sig.SignerCertificate.Subject) ($($sig.Status)), timestamp: $([bool]$sig.TimeStamperCertificate)"
}
$global:LASTEXITCODE = 0
