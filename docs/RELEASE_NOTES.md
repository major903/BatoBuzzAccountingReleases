# Release Notes - BatoBuzz Accounting v1.0.3

## v1.0.3

- Fixed startup for existing SQLite databases created before configurable TDS rates were added.
- Added the missing `TdsRates` schema upgrade and retained compatibility with older backups.

# Release Notes - BatoBuzz Accounting v1.0.2

## v1.0.2

- Added the missing architecture documentation.
- Included `docs/architecture/ARCHITECTURE.md` in the Windows installer.

# Release Notes - BatoBuzz Accounting v1.0.1

## v1.0.1

- Fixed the Cash Flow report SQLite reversal-link error.
- Added a schema migration for existing local databases.
- Improved the main tab strip so open tabs remain visible with horizontal scrolling.

# Release Notes - BatoBuzz Accounting v1.0.0

## Release Date
July 13, 2026

## What's Included

### Implemented Features
- **Company Management**: Create company, set financial year, PAN/VAT
- **Chart of Accounts**: Multi-level account groups, ledgers, opening balances
- **Journal Entries**: Full double-entry with debit/credit validation, including Credit Notes and Debit Notes
- **Sales Invoices**: Multi-line invoices, discount, VAT calculation
- **Purchase Bills**: Multi-line bills, VAT tracking
- **Customers & Suppliers**: Contact management with Nepal PAN/VAT
- **Receipts & Payments**: Payment recording, with optional TDS withholding on payments
- **Inventory**: Items, categories, warehouses, stock movements
- **Reports**: Trial Balance, P&L, Balance Sheet, General Ledger, Ageing
- **Dashboard**: KPI cards with financial summary
- **Authentication**: User login with offline support
- **Nepal Support**: NPR currency, 13% VAT, Shrawan-Ashad fiscal year, AD/BS date display
- **Printing**: PDF templates for sales invoices, purchase bills, receipts, payment vouchers, and journal vouchers
- **Export**: Real .xlsx (ClosedXML) and .pdf (PdfSharp) export for all reports
- **Backup/Restore**: One-click backup, restore with automatic safety copy and confirmation prompt
- **Corrections**: Dedicated workspace for dated invoice/bill cancellation and
  receipt/payment/manual-journal reversal, with mandatory reasons and audit history
- **Period Control**: Posting locks and automatic next-financial-year creation

### Architecture
- Clean Architecture with 6 projects
- Entity Framework Core with SQLite (desktop), SQLite or PostgreSQL (API)
- WPF Desktop with MVVM (light theme)
- ASP.NET Core Web API with Swagger
- Docker deployment definition with a loopback-only nginx endpoint by default
- Explicit PostgreSQL Compose override requiring an operator-supplied password
- Windows installer definition (Inno Setup 6 or 7 required to build it)

### Release Engineering

- Windows CI restores and builds the solution with warnings treated as errors
- CI and local publish scripts run the SQLite accounting smoke check
- The smoke suite covers double-entry balance, VAT, TDS, weighted-average stock,
  allocations, ageing, corrections, period locks, rollover, access isolation,
  login lockout, backup validation, and PDF/Excel readback
- Release scripts fail on native tool errors and clean stale artifact folders
- Release builds emit a SHA-256 manifest for the executable and installer
- The API container runs as a fixed non-root user and has no direct host port

### Documentation
- Architecture guide
- VPS deployment guide
- Nepal accounting rules
- Security documentation
- Quick start guide

## Known Limitations
- TDS structure is implemented (Settings > Tax Settings) but ships with no
  rates pre-loaded -- accountants must add and verify their own
- Full Nepali language UI not implemented (out of scope by request)
- Manufacturing module not implemented
- Payroll module requires Nepal statutory verification
- Bank reconciliation currently supports manual journal-line clearing; bank-statement
  import, matching, and finalized reconciliation sessions are not yet implemented
- Multi-currency not yet supported (NPR only)
- The desktop release is single-owner. Shared-company memberships and delegated
  desktop roles are not part of v1.0
- PostgreSQL support is API-only and is exercised by the container CI matrix;
  each production PostgreSQL environment still requires deployment-specific
  backup, restore, TLS, and performance validation
- Authenticode signing and SBOM generation are release-operator steps and are
  not automated by the repository
- SQLite and PostgreSQL upgrades are versioned. Upgrades must still be rehearsed
  on copies of customer databases before a production rollout

## System Requirements
- Windows 10/11 (desktop)
- Microsoft Edge WebView2 Runtime for report display and printing
- No separate .NET runtime for the self-contained release build
- 4GB RAM minimum
- 500MB disk space

## What's Next (v1.1)
- Shared-company users, roles, and permissions
- Bank-statement import and automatic reconciliation matching
- Multi-currency support
- Payroll module (pending Nepal statutory verification)
