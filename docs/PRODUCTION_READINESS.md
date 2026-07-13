# Production Readiness Record

Validation date: July 13, 2026  
Target: BatoBuzz Accounting 1.0.0, Windows x64 desktop and ASP.NET Core API

## Verified in this workspace

- Release solution build succeeds with zero warnings and zero errors.
- The automated accounting smoke suite passes on a fresh SQLite database.
- Debits equal credits after sales, purchases, VAT, TDS, receipts, payments,
  inventory changes, corrections, manual journals, and year rollover.
- Weighted-average stock value reconciles to Stock-in-Hand and cost of goods sold.
- Draft and reversed entries are handled correctly by ledgers and reports.
- Trial Balance, General Ledger, Profit and Loss, Balance Sheet, cash flow,
  registers, ageing, dashboard, and stock report paths are exercised.
- Excel export is reopened with ClosedXML and PDF output is signature-checked.
- Receipt/payment reversal and invoice/bill cancellation restore their party,
  allocation, journal, and inventory effects and preserve an audit record.
- Period locks, invalid dates, duplicate corrections, unsafe purchase-stock
  reversal, cross-company references, control-ledger journals, negative values,
  and unbalanced journals are rejected.
- Password change, case-insensitive login, five-attempt lockout, credit limits,
  company ownership, and acting-user audit identity are exercised.
- SQLite upgrades are idempotent, operational journal links are backfilled, and
  an online safety backup is made before an upgrade.
- A validated backup opens successfully; an invalid backup is rejected.
- The desktop was exercised as a user on an isolated fresh database through
  owner creation, company setup, dashboard, contacts, inventory, reports,
  remembered login, and the corrections workspace.
- The API was exercised live for health, 401 protection, registration/login,
  company access isolation, company details, Swagger, and accounting history
  and correction routes.
- The dependency vulnerability query reported no known vulnerable NuGet
  packages at validation time.
- The self-contained Windows x64 executable starts successfully from `publish`;
  its SHA-256 is recorded in `dist/SHA256SUMS.txt` and was verified after build.

## Release commands

```powershell
dotnet build BatoBuzz.Accounting.sln -c Release
dotnet run --project scripts/BatoBuzz.SmokeTests/BatoBuzz.SmokeTests.csproj -c Release
dotnet list BatoBuzz.Accounting.sln package --vulnerable --include-transitive
.\scripts\build-release.ps1
```

The last command requires Inno Setup 6 or 7. Use `-SkipInstaller` only to make a
portable validation build.

## Required operator gates before production distribution

These cannot be supplied by source code or local accounting tests:

1. Install Inno Setup and build the installer. This validation machine did not
   have `ISCC.exe`, so the portable executable was produced but no setup EXE.
2. Authenticode-sign the application, installer, and uninstaller with the
   publisher certificate; verify them on a clean Windows 10 and Windows 11 PC.
3. Perform malware scanning, generate/retain an SBOM, and archive the reviewed
   source revision, CI log, checksum manifest, and signing record.
4. Have a qualified Nepal accountant approve the chart of accounts, VAT/TDS
   configuration, opening balances, invoice wording, and statutory reports for
   the actual business and current law.
5. Rehearse upgrade and restore using anonymized copies of each production
   database, then reconcile Trial Balance, party ageing, stock, and key ledgers.
6. For an API deployment, validate TLS, firewall, secrets, PostgreSQL backup and
   restore, monitoring, and load expectations in that exact environment.

## Supported-scope boundaries

- Desktop 1.0 is single-owner and offline-first. It is not a concurrent shared
  SQLite or delegated multi-user desktop product.
- Inventory costing is weighted average. Manufacturing, payroll, multi-currency,
  and automatic bank-statement matching are outside the 1.0 scope.
- Desktop SQLite is not application-encrypted; production devices need Windows
  encryption, access controls, secure backups, and normal endpoint protection.

