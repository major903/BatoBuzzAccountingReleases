# BatoBuzz accounting audit — 2026-07-15

## Summary

The reproducible local audit passed: the Release solution build, fresh SQLite
accounting smoke suite, vulnerability scan, API health/authorization check,
desktop startup check, self-contained publish, and Inno Setup compilation all
succeeded. No product defect was observed in those checks.

This is **not a production or statutory sign-off**. A clean Windows install,
update, and uninstall run, live Docker provider checks, and review by a Nepal
accountant remain required before the full acceptance criteria can be marked
complete.

## Results

| Area | Result | Evidence |
| --- | --- | --- |
| Release solution and XAML compilation | Pass | `dotnet build BatoBuzz.Accounting.sln -c Release --nologo`: 0 warnings, 0 errors. The WPF project (24 XAML files) compiled. |
| Fresh accounting and recovery smoke suite | Pass | Fresh temporary SQLite database; output `BatoBuzz smoke test passed.` Trial Balance: Dr 1,603.00 / Cr 1,603.00. |
| Accounting controls in smoke suite | Pass | The suite validates VAT/TDS posting, inventory costing, receipt/payment allocation, unbalanced/manual-control-ledger rejection, corrections, reversal audit history, period lock, rollover report non-mutation, and a balanced balance sheet. |
| Backup, staged restore, and exports | Pass | The suite creates and validates an online backup, rejects an invalid backup, checks daily automatic backup behavior and schema-upgrade idempotence, and reads back Excel and PDF exports. A separate isolated restart restored a consistent backup into a fresh desktop data directory; the restore marker was removed and the restored owner/company could log in through the API. |
| Dependency vulnerability scan | Pass | `dotnet list BatoBuzz.Accounting.sln package --vulnerable --include-transitive`: no vulnerable packages reported by NuGet. |
| API readiness and authorization | Pass | Isolated SQLite API: `/health` = Ready; `/health/live` = Alive; unauthenticated `/api/companies` = 401; a second test owner accessing the first owner's company = 403. |
| Desktop startup and isolated persistence | Pass | Published `BatoBuzz.Desktop.exe` stayed running, created only its `BATOBUZZ_DATA_DIRECTORY` test database, and created no startup-error log. |
| Release script and installer compilation | Pass | `scripts/build-release.ps1` completed and Inno Setup 6.7.3 generated the setup program. |
| Artifact integrity | Pass | SHA-256 manifest matches the published executable and installer (below). |

## Release artifacts

- [Installer](../dist/BatoBuzzAccounting_Setup_v1.0.8.exe)
- [Checksum manifest](../dist/SHA256SUMS.txt)

```
5054bd003c4caaa87e0d5a9d8801759b0d8cdd8b56b90d5c5546c623b0775b02  publish/BatoBuzz.Desktop.exe
dd819d327b7a117f51cc5bd70645f894ca1978774409d429a1d3376d2ca2e6e9  dist/BatoBuzzAccounting_Setup_v1.0.8.exe
```

## Open acceptance work (not defects)

| Required check | Status | Why it remains open |
| --- | --- | --- |
| Clean Windows 10/11 install, update, and uninstall | Not run | This host is not a clean VM. Run the installer on a clean Windows machine; verify Start-menu entry, optional desktop shortcut, taskbar icon, uninstall, data persistence through update, and fresh-database recovery. |
| Interactive desktop journey | Partially covered | Startup, compilation, navigation wiring, and workflow services are covered, but all menu/tab clicks, dialog wording, printing, and visual usability need a human test pass using `docs/user-guide/TESTER_GUIDE.md`. |
| Docker SQLite and PostgreSQL runtime health | Not run | Docker is not installed on this host. Validate both Compose configurations with a protected test `.env`, then check `/health` and container health states. |
| Nepal VAT/TDS compliance and accountant workflow sign-off | Not run | Requires a Nepal accountant and current official IRD process guidance. Use a fresh test company only and expand the tester guide with VAT/TDS, stock valuation, corrections, period-lock, and report-reconciliation scenarios. |

## Issue report

No Critical, High, Medium, or Low product issue was reproduced by the automated
and isolated checks above. The open items are external validation gates, not
defects or release approval.

## Reproduction commands

```powershell
dotnet build BatoBuzz.Accounting.sln -c Release --nologo
dotnet run --project scripts\BatoBuzz.SmokeTests\BatoBuzz.SmokeTests.csproj -c Release --no-restore
dotnet list BatoBuzz.Accounting.sln package --vulnerable --include-transitive
.\scripts\build-release.ps1
```

For the manual portion, use [the tester guide](user-guide/TESTER_GUIDE.md) with
a fresh test company. Do not use live company data, PAN/VAT information, bank
details, or production database backups.
