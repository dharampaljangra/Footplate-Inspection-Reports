<#
  Creates a code-signing certificate for the Windows build (self-signed).
  For a publicly trusted signature, buy an Authenticode (OV/EV) code-signing certificate from a CA
  and use it with sign-windows.ps1 instead.
  Usage:  .\build\create-signing-certificate.ps1 -Password "YourStrongPassword"
#>
param([Parameter(Mandatory)][string]$Password,
      [string]$Subject = "CN=DHARAM PAL JANGID, O=Senior Section Engineer (P.Way) Kota Division of WCRly, C=IN")
$ErrorActionPreference = "Stop"
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -KeyAlgorithm RSA -KeyLength 3072 `
        -HashAlgorithm SHA256 -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5)
$pwd = ConvertTo-SecureString $Password -AsPlainText -Force
New-Item -ItemType Directory -Force -Path "$PSScriptRoot\..\artifacts" | Out-Null
Export-PfxCertificate -Cert $cert -FilePath "$PSScriptRoot\..\artifacts\Footplate-signing.pfx" -Password $pwd | Out-Null
Export-Certificate -Cert $cert -FilePath "$PSScriptRoot\..\artifacts\Footplate-signing.cer" | Out-Null
Write-Host "Created artifacts\Footplate-signing.pfx (keep it private) and Footplate-signing.cer (install on PCs as 'Trusted Publisher' and 'Trusted Root')."
Write-Host "Thumbprint: $($cert.Thumbprint)"
