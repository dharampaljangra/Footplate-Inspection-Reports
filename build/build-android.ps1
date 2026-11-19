<#
  Builds a signed Android APK.
  First time only, create a keystore (keep it safe - the same key is needed for every update):
    keytool -genkeypair -v -keystore artifacts\footplate.keystore -alias footplate -keyalg RSA -keysize 3072 -validity 10000 `
            -dname "CN=DHARAM PAL JANGID, O=Senior Section Engineer (P.Way) Kota Division of WCRly, C=IN"
  Usage:  .\build\build-android.ps1 -KeystorePassword "..."
#>
param([string]$Keystore = "artifacts\footplate.keystore", [string]$Alias = "footplate", [Parameter(Mandatory)][string]$KeystorePassword)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
Push-Location $root
try {
  dotnet test tests\FootplateIR.Core.Tests\FootplateIR.Core.Tests.csproj -c Release
  if ($LASTEXITCODE -ne 0) { throw "Unit tests failed - build stopped." }
  $ks = Resolve-Path $Keystore
  dotnet publish src\FootplateIR.App\FootplateIR.App.csproj -c Release -f net10.0-android -o artifacts\android `
    -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="$ks" -p:AndroidSigningKeyAlias=$Alias `
    -p:AndroidSigningKeyPass=$KeystorePassword -p:AndroidSigningStorePass=$KeystorePassword
  if ($LASTEXITCODE -ne 0) { throw "Android publish failed." }
  $apk = Get-ChildItem artifacts\android -Filter *-Signed.apk | Select-Object -First 1
  Copy-Item $apk.FullName artifacts\Footplate-Reports-Android.apk -Force
  Get-FileHash artifacts\Footplate-Reports-Android.apk -Algorithm SHA256 | Format-List
  Write-Host "Done: artifacts\Footplate-Reports-Android.apk  (copy to the phone and install; allow 'install unknown apps' once)."
} finally { Pop-Location }
