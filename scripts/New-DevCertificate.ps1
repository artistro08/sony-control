<#
    Creates the self-signed certificate that signs the Sony Control MSIX and trusts it
    on this PC.

    Outputs the certificate thumbprint (Build-Package.ps1 reads it from the store) and
    SonyControl.cer in the repo root. Run once, from an elevated PowerShell: trusting a
    certificate for sideloading means adding it to Local Machine > Trusted People.

    Depends on the PKI module that ships with Windows:
    https://learn.microsoft.com/powershell/module/pki/new-selfsignedcertificate
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# Must match Publisher in src/SonyControl.App/Package.appxmanifest
$subject = 'CN=Devin Green'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

# Reuse an existing signing certificate
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) -and $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3' } |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Sony Control package signing' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(3) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}

# Trust It For Sideloading
$cerPath = Join-Path $root 'SonyControl.cer'
Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

Write-Output "Signing certificate: $($certificate.Thumbprint)"
