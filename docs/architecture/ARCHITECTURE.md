# BatoBuzz Accounting Architecture

## Overview

BatoBuzz Accounting is a Windows WPF desktop application built with .NET 10. The desktop edition uses a local SQLite database and does not require the ASP.NET Core API to run.

```text
WPF Desktop UI
    ↓
ViewModels and Desktop Services
    ↓
Application Services
    ↓
Infrastructure Repositories / EF Core
    ↓
SQLite database (%LOCALAPPDATA%\BatoBuzz\Accounting\BatoBuzz.db)
```

## Projects

- `BatoBuzz.Desktop`: WPF screens, themes, commands, WebView2 report rendering, local session and storage handling.
- `BatoBuzz.Application`: accounting, sales, purchasing, inventory, authentication, reporting, and business workflows.
- `BatoBuzz.Domain`: entities, value objects, enums, domain validation, posting and reversal rules.
- `BatoBuzz.Infrastructure`: Entity Framework Core SQLite context, repositories, unit of work, and schema upgrades.
- `BatoBuzz.Contracts`: request and response DTOs shared by the desktop client and API.
- `BatoBuzz.Api`: optional ASP.NET Core API for server-backed deployments.

## Data and migrations

The desktop app creates the initial schema with EF Core `EnsureCreated`. `SchemaUpgrader.ApplyAll` then applies ordered, idempotent upgrades recorded in `__BatoBuzzSchemaMigrations`. Before pending upgrades, it creates a safety copy of the database. Existing installations therefore receive additive schema changes when the application starts.

## Accounting flow

Transactions are created through application services, validated by domain entities, and persisted through repositories and the unit of work. Posted journal entries contain balanced debit and credit lines. Sales, purchases, receipts, and payments link back to their posted journal entries so reports and corrections use the same accounting records.

## Reporting

Report view models call application services and render HTML into Microsoft Edge WebView2. Reports can be exported to PDF or Excel. The Cash Flow report derives inflows and outflows from cash and bank ledger transactions.

## Release packaging

The Windows release is a self-contained `win-x64` publish. Inno Setup packages the published files into a setup executable with Start Menu, optional desktop, and uninstall entries. Release artifacts are published through GitHub Releases with SHA-256 checksums.
