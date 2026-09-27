# Publishes the Windows client as an unpackaged, framework-dependent build (no MSIX). Requires
# the .NET 10 Desktop Runtime on the target machine (see README.md) - self-contained publish is
# not used here because it currently fails on this project with NU1102 (tries to resolve a Mono
# runtime pack for win-x64, which doesn't apply to a WinUI/CoreCLR desktop target).
# Run from anywhere; paths are resolved relative to this script's location.
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
    -p:WindowsPackageType=None `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit code $LASTEXITCODE) - see errors above."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Published to: $outDir"
Write-Host "Distributable executable: $outDir\PasswordManager.Maui.exe"
