# Publishes the Windows client as a self-contained, unpackaged win10-x64 build (no MSIX, no
# .NET runtime required on the target machine). Run from anywhere; paths are resolved relative
# to this script's location.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
$csproj = Join-Path $repoRoot "src\PasswordManager.Maui\PasswordManager.Maui.csproj"
$outDir = Join-Path $PSScriptRoot "output"

dotnet publish $csproj `
    -f net10.0-windows10.0.19041.0 `
    -c $Configuration `
    -r win10-x64 `
    --self-contained true `
    -p:WindowsPackageType=None `
    -o $outDir

Write-Host ""
Write-Host "Published to: $outDir"
Write-Host "Distributable executable: $outDir\PasswordManager.Maui.exe"
