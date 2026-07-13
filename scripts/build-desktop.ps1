# BatoBuzz Accounting - Desktop Build Script
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$OutputPath = "publish",
    [switch]$SelfContained,
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $true
}

function Invoke-NativeCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$ArgumentList = @()
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE`: $FilePath $($ArgumentList -join ' ')"
    }
}

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPath = Join-Path $ProjectRoot "src\BatoBuzz.Desktop\BatoBuzz.Desktop.csproj"
$SmokeProjectPath = Join-Path $ProjectRoot "scripts\BatoBuzz.SmokeTests\BatoBuzz.SmokeTests.csproj"

$outputCandidate = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $ProjectRoot $OutputPath
}
$OutputDirectory = [System.IO.Path]::GetFullPath($outputCandidate)
$projectRootPrefix = $ProjectRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $OutputDirectory.StartsWith($projectRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be a directory inside the project root: $ProjectRoot"
}

$protectedDirectories = @(
    $ProjectRoot,
    (Join-Path $ProjectRoot ".git"),
    (Join-Path $ProjectRoot ".github"),
    (Join-Path $ProjectRoot "deployment"),
    (Join-Path $ProjectRoot "docs"),
    (Join-Path $ProjectRoot "installer"),
    (Join-Path $ProjectRoot "scripts"),
    (Join-Path $ProjectRoot "src")
)
foreach ($protectedDirectory in $protectedDirectories) {
    $protectedPrefix = $protectedDirectory.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($OutputDirectory.Equals($protectedDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        $OutputDirectory.StartsWith($protectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean protected project directory: $OutputDirectory"
    }
}

Write-Host "=== BatoBuzz Desktop Build ===" -ForegroundColor Cyan

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -Path $OutputDirectory -ItemType Directory | Out-Null

$dotnetCommand = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$dotnetVersion = & $dotnetCommand --version
if ($LASTEXITCODE -ne 0) {
    throw "Unable to query the selected .NET SDK. dotnet exited with code $LASTEXITCODE."
}
if ($dotnetVersion -notmatch '^10\.') {
    throw ".NET 10 SDK is required. Installed SDK selected by global.json: $dotnetVersion"
}

Write-Host "Restoring and running the accounting smoke check..." -ForegroundColor Yellow
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "restore", $SmokeProjectPath, "--nologo"
)
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "run", "--project", $SmokeProjectPath, "--configuration", $Configuration, "--no-restore"
)

Write-Host "Restoring Windows x64 runtime packages..." -ForegroundColor Yellow
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "restore", $ProjectPath, "--runtime", "win-x64", "--nologo"
)

$publishArguments = @(
    "publish", $ProjectPath,
    "--configuration", $Configuration,
    "--output", $OutputDirectory,
    "--runtime", "win-x64",
    "--self-contained", $SelfContained.IsPresent.ToString().ToLowerInvariant(),
    "--no-restore",
    "--nologo"
)
if ($SingleFile) {
    $publishArguments += "-p:PublishSingleFile=true"
    $publishArguments += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

Write-Host "Publishing desktop application ($Configuration)..." -ForegroundColor Yellow
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList $publishArguments

$exe = Join-Path $OutputDirectory "BatoBuzz.Desktop.exe"
if (!(Test-Path -LiteralPath $exe) -or (Get-Item -LiteralPath $exe).Length -eq 0) {
    throw "Publish completed but BatoBuzz.Desktop.exe was not found: $exe"
}

Write-Host "Build complete. Output: $OutputDirectory" -ForegroundColor Green
