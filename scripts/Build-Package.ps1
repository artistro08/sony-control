<#
    Builds the signed Sony Control MSIX for x64 and ARM64.

    Outputs .msix files under src/SonyControl.App/AppPackages. Used to produce the
    installable app. Run scripts/New-DevCertificate.ps1 once first.

    Depends on Visual Studio 2026 (MSBuild, found with vswhere:
    https://github.com/microsoft/vswhere) and the Windows App SDK MSIX tooling.
#>

[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string[]] $Platforms = @('x64', 'ARM64')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

# Find The Signing Certificate
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq 'CN=Devin Green' -and $_.NotAfter -gt (Get-Date) -and $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3' } |
    Select-Object -First 1
if (-not $certificate) {
    throw 'No signing certificate. Run scripts/New-DevCertificate.ps1 from an elevated PowerShell first.'
}

# Find MSBuild
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

# Find SignTool (ships with the Windows SDK build tools NuGet package)
$signtool = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') -Recurse -Filter signtool.exe |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $signtool) {
    throw 'SignTool not found. Restore the solution once so the Windows SDK build tools package is downloaded.'
}

# Build And Package
foreach ($platform in $Platforms) {
    & $msbuild (Join-Path $root 'SonyControl.sln') -restore -p:RestorePackagesConfig=true `
        -p:Configuration=Release `
        -p:Platform=$platform `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxBundle=Never `
        -p:UapAppxPackageBuildMode=SideloadOnly `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateThumbprint=$($certificate.Thumbprint) `
        -p:AppExeSigningThumbprint=$($certificate.Thumbprint) `
        -p:AppExeSignTool=$($signtool.FullName) `
        -m -nologo -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Packaging failed for $platform ($LASTEXITCODE)."
    }
}

Get-ChildItem (Join-Path $root 'src\SonyControl.App\AppPackages') -Recurse -Filter *.msix | ForEach-Object { Write-Output $_.FullName }
