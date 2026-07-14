# BatoBuzz Accounting - Windows x64 release and installer build
[CmdletBinding()]
param(
    [switch]$SkipInstaller
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
$ProjectFile = Join-Path $ProjectRoot "src\BatoBuzz.Desktop\BatoBuzz.Desktop.csproj"
$SmokeProjectFile = Join-Path $ProjectRoot "scripts\BatoBuzz.SmokeTests\BatoBuzz.SmokeTests.csproj"
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$InstallerScript = Join-Path $ProjectRoot "installer\BatoBuzzAccounting.iss"
$BuildPropsFile = Join-Path $ProjectRoot "Directory.Build.props"
$ChecksumOutput = Join-Path $DistDir "SHA256SUMS.txt"

[xml]$buildProperties = Get-Content -LiteralPath $BuildPropsFile -Raw
$versionNode = $buildProperties.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Directory.Build.props does not define a product Version."
}
$productVersion = $versionNode.InnerText.Trim()
$installerSource = Get-Content -LiteralPath $InstallerScript -Raw
$installerVersionMatch = [regex]::Match($installerSource, '(?m)^#define MyAppVersion "([^"]+)"\s*$')
if (-not $installerVersionMatch.Success -or $installerVersionMatch.Groups[1].Value -ne $productVersion) {
    throw "Installer version must match Directory.Build.props Version ($productVersion)."
}
$InstallerOutput = Join-Path $DistDir "BatoBuzzAccounting_Setup_v$productVersion.exe"

Write-Host "=== BatoBuzz Accounting Windows Release ===" -ForegroundColor Cyan

foreach ($directory in @($PublishDir, $DistDir)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    New-Item -Path $directory -ItemType Directory | Out-Null
}

Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
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
    "restore", $SmokeProjectFile, "--nologo"
)
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "run", "--project", $SmokeProjectFile, "--configuration", "Release", "--no-restore"
)

Write-Host "Restoring Windows x64 runtime packages..." -ForegroundColor Yellow
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "restore", $ProjectFile, "--runtime", "win-x64", "--nologo"
)

Write-Host "Publishing self-contained Windows x64 application..." -ForegroundColor Yellow
Invoke-NativeCommand -FilePath $dotnetCommand -ArgumentList @(
    "publish", $ProjectFile,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", "true",
    "--output", $PublishDir,
    "--no-restore",
    "--nologo",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:PublishTrimmed=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

$exe = Join-Path $PublishDir "BatoBuzz.Desktop.exe"
if (!(Test-Path -LiteralPath $exe) -or (Get-Item -LiteralPath $exe).Length -eq 0) {
    throw "Publish completed but BatoBuzz.Desktop.exe was not found."
}

Write-Host "Application published to: $PublishDir" -ForegroundColor Green

$artifacts = @($exe)
if ($SkipInstaller) {
    Write-Host "Installer step skipped." -ForegroundColor Yellow
} else {
    $isccCandidates = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

    if (-not $iscc) {
        throw "Inno Setup 6 or 7 was not found. Install it, or explicitly use -SkipInstaller for a portable-only build."
    }

    Write-Host "Building Windows installer..." -ForegroundColor Yellow
    Invoke-NativeCommand -FilePath $iscc -ArgumentList @($InstallerScript)
    if (!(Test-Path -LiteralPath $InstallerOutput) -or (Get-Item -LiteralPath $InstallerOutput).Length -eq 0) {
        throw "Inno Setup completed but the expected installer was not found: $InstallerOutput"
    }
    $artifacts += $InstallerOutput
    Write-Host "Installer created: $InstallerOutput" -ForegroundColor Green
}

$checksumLines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    $relativePath = $artifact.Substring($ProjectRoot.Length).TrimStart(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
    "$hash *$relativePath"
}
$checksumLines | Set-Content -LiteralPath $ChecksumOutput -Encoding ascii

Write-Host "SHA-256 manifest created: $ChecksumOutput" -ForegroundColor Green
Write-Host "Release build completed successfully." -ForegroundColor Green
