# Publishes a signed, installable Android APK. Requires a keystore - see README.md to generate
# one first. Run from anywhere; paths are resolved relative to this script's location.
param(
    [string]$Configuration = "Release",
    [Parameter(Mandatory = $true)][string]$KeystorePath,
    [Parameter(Mandatory = $true)][string]$KeystoreAlias,
    [Parameter(Mandatory = $true)][securestring]$KeystorePassword
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$csproj = Join-Path $repoRoot "src\PasswordManager.Maui\PasswordManager.Maui.csproj"
$outDir = Join-Path $PSScriptRoot "output"

$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($KeystorePassword))

dotnet publish $csproj `
    -f net10.0-android `
    -c $Configuration `
    -p:AndroidKeyStore=true `
    -p:AndroidSigningKeyStore=$KeystorePath `
    -p:AndroidSigningKeyAlias=$KeystoreAlias `
    -p:AndroidSigningKeyPass=$plainPassword `
    -p:AndroidSigningStorePass=$plainPassword `
    -o $outDir

Write-Host ""
Write-Host "Published to: $outDir"
Write-Host "Signed APK: look for *-Signed.apk in $outDir"
