# Run and Build BatoBuzz Accounting on Windows

## What this project is

This is a Windows-only WPF desktop application built with C# and .NET 10. The desktop app uses a local SQLite database and can run without starting the ASP.NET Core API.

## Requirements

1. Windows 10 or Windows 11, 64-bit.
2. .NET 10 SDK.
3. Visual Studio with the **.NET desktop development** workload, or VS Code with C# Dev Kit.
4. Inno Setup 6 or 7 only when producing the final installer.

Confirm the SDK in PowerShell:

```powershell
dotnet --list-sdks
```

At least one `10.0.x` SDK must appear.

## Run locally from PowerShell

Open PowerShell in the folder containing `BatoBuzz.Accounting.sln`:

```powershell
dotnet restore .\BatoBuzz.Accounting.sln
dotnet build .\BatoBuzz.Accounting.sln -c Debug
dotnet run --project .\src\BatoBuzz.Desktop\BatoBuzz.Desktop.csproj
```

The first launch creates the SQLite database here:

```text
%LOCALAPPDATA%\BatoBuzz\Accounting\BatoBuzz.db
```

On first run, create the owner account with a username of at least three
characters and a password of at least eight characters. Later launches verify
that stored credential and apply account lockout rules.

## Run using Visual Studio

1. Open `BatoBuzz.Accounting.sln`.
2. Right-click `BatoBuzz.Desktop` and choose **Set as Startup Project**.
3. Select **Debug** and **Any CPU** or **x64**.
4. Press `F5`.

Do not select `BatoBuzz.Api` as the startup project when your goal is to open the Windows application.

## Create a distributable EXE and installer

From the solution root, run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-release.ps1
```

Outputs:

```text
publish\BatoBuzz.Desktop.exe

dist\BatoBuzzAccounting_Setup_v1.0.0.exe

dist\SHA256SUMS.txt
```

The script cleans `publish` and `dist`, runs the accounting smoke check, and
fails on restore, test, publish, or installer errors. Inno Setup is required
unless `-SkipInstaller` is explicitly selected. The published desktop EXE is
self-contained for Windows x64, so the customer should not need to install .NET
separately. The setup EXE installs under Program Files and creates Start Menu
shortcuts.

To create only the portable published application and skip Inno Setup:

```powershell
.\scripts\build-release.ps1 -SkipInstaller
```

## Application and installer icon

Place a valid multi-size Windows icon at:

```text
src\BatoBuzz.Desktop\Assets\BatoBuzz.ico
```

The desktop project and Inno Setup script both use this file. Keep it present
and validate the icon on the executable, installer, Start menu, and uninstaller.

## Important release warning

Creating an EXE does not make the accounting product production-ready. Before selling or deploying it, validate database upgrades, backups, licensing, authentication, permissions, audit trails, Nepal tax/accounting calculations, error logging, signing, automated tests, and recovery from corrupted or interrupted transactions.

The scripts create SHA-256 checksums but do not Authenticode-sign artifacts.
Production releases still require application and installer signing, a clean-VM
installation test, malware scanning, and preservation of the CI build record.
