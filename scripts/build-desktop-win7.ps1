# BatoBuzz Accounting - Windows 7 SP1 Legacy Build
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$OutputPath = "publish-win7"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = Join-Path $ProjectRoot "src\BatoBuzz.Desktop\BatoBuzz.Desktop.csproj"
$OutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    [System.IO.Path]::GetFullPath($OutputPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $OutputPath))
}
$rootPrefix = $ProjectRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

if (-not $OutputDirectory.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputDirectory.Equals($ProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be a directory inside the project root, not the root itself."
}

Write-Host "Building BatoBuzz Accounting Windows 7 legacy edition..." -ForegroundColor Cyan
& dotnet build $ProjectPath --configuration $Configuration --output $OutputDirectory `
    -p:Windows7Legacy=true -p:RestoreLockedMode=false --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Windows 7 legacy build failed."
}

$exe = Join-Path $OutputDirectory "BatoBuzz.Desktop.exe"
if (-not (Test-Path -LiteralPath $exe) -or (Get-Item -LiteralPath $exe).Length -eq 0) {
    throw "Build completed but BatoBuzz.Desktop.exe was not produced."
}

Write-Host "Windows 7 legacy build complete: $OutputDirectory" -ForegroundColor Green
