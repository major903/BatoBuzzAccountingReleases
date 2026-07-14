# BatoBuzz Accounting

An offline-first Windows accounting and business management application for Nepal.

## Features

### Core Accounting
- Double-entry bookkeeping with balanced journal entries
- Chart of accounts with multi-level groups
- Sales, Purchase, Receipt, Payment, Contra, and Journal vouchers
- Debit notes and Credit notes
- Opening balance entry
- Fiscal period management
- Nepal fiscal year support (Shrawan to Ashad)

### Sales & Receivables
- Customer management with PAN/VAT
- Sales invoices with item lines, discount, and VAT
- Payment receipts with invoice allocation
- Receivables ageing report

### Purchases & Payables
- Supplier management with PAN/VAT
- Purchase bills with item lines and VAT
- Supplier payments with bill allocation
- Payables ageing report

### Inventory
- Items (goods, services, assets)
- Categories and units
- Warehouse-based stock balances with a default warehouse
- Stock movements (opening, in, out, return, damage, write-off, and adjustment)
- Weighted-average costing with exact-value correction entries
- Stock valuation reports
- Low stock alerts

### Reports
- Trial Balance
- Profit & Loss
- Balance Sheet
- General Ledger
- Sales Register
- Purchase Register
- Receivables/Payables Ageing
- Stock Summary

### Nepal-Specific
- Nepalese Rupee (NPR) currency
- PAN number support
- VAT registration and 13% VAT calculation
- Bikram Sambat date display (ready)
- Nepal provinces
- Nepal fiscal year (Shrawan-Ashad)

### Technical
- Offline-first Windows desktop operation
- SQLite local database
- ASP.NET Core Web API backend
- PostgreSQL API configuration through an explicit, password-required opt-in
- Hardened Docker deployment with a loopback-only reverse proxy by default
- Clean Architecture
- Theme-aware Windows UI

### Controls and Recovery
- Dated reversals and cancellations with mandatory reasons and audit history
- Period locks and automatic Nepal-style financial-year rollover
- Validated SQLite backup, staged restore, and pre-upgrade safety copies
- Password hashing, login lockout, and company ownership enforcement

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Desktop UI | C# .NET 10, WPF, CommunityToolkit.Mvvm |
| Application | Clean Architecture, CQRS-ready |
| Domain | DDD Entities, Value Objects, Events |
| Persistence | EF Core 10, SQLite, PostgreSQL |
| API | ASP.NET Core 10, Swagger |
| Deployment | Docker, Nginx, Linux VPS |

## Project Structure

```
BatoBuzz.Accounting.sln
src/
  BatoBuzz.Domain/          # Entities, Enums, Value Objects, Events, Exceptions
  BatoBuzz.Contracts/        # Request/Response DTOs, API contracts
  BatoBuzz.Application/      # Service interfaces and implementations
  BatoBuzz.Infrastructure/   # EF Core DbContext, Repositories, UoW
  BatoBuzz.Desktop/          # WPF Application (MVVM)
  BatoBuzz.Api/              # ASP.NET Core Web API
deployment/
  docker/                    # Dockerfile, docker-compose.yml
  nginx/                     # Reverse proxy config
  scripts/                   # VPS setup, backup scripts
installer/                   # Inno Setup script
scripts/                     # Build scripts
docs/                        # Documentation
```

## Getting Started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 or VS Code
- Windows 10/11 (for desktop)
- Docker (optional, for API deployment)

### Build Desktop Application
```bash
# Restore packages
dotnet restore BatoBuzz.Accounting.sln

# Build solution
dotnet build BatoBuzz.Accounting.sln -c Release

# Run desktop app
dotnet run --project src/BatoBuzz.Desktop

# Or publish a framework-dependent Windows x64 build. The script cleans its
# output directory and runs the accounting smoke check before publishing.
.\scripts\build-desktop.ps1 -Configuration Release

# Produce the self-contained release and Inno Setup installer. This command
# fails if the smoke check, publish, or requested installer step fails.
.\scripts\build-release.ps1
```

### Run API
```bash
cd src/BatoBuzz.Api
dotnet run
```
API will be available at `http://localhost:5000`
Swagger UI at `http://localhost:5000/swagger`

The desktop edition is intentionally a single-owner product in this release.
Do not use one local database as a concurrent multi-user data file.

### Docker Deployment

The default Compose file uses SQLite and publishes only nginx on host loopback
at `http://127.0.0.1:8080`; the API has no direct host port.

```bash
cd deployment/docker
mkdir -p data logs
sudo chown -R 10001:10001 data logs
umask 077
if [[ ! -s .env ]]; then
  read -rsp "API signing key (32+ bytes): " API_SIGNING_KEY
  echo
  printf 'API_SIGNING_KEY=%s\n' "$API_SIGNING_KEY" > .env
  unset API_SIGNING_KEY
fi
docker compose config --quiet
docker compose up -d --build
curl http://127.0.0.1:8080/health
```

PostgreSQL is an explicit override. It is never started by the default command
and has no built-in password. After creating the protected `.env` above, add the
database secret without placing it in shell history:

```bash
cd deployment/docker
if ! grep -q '^POSTGRES_PASSWORD=' .env; then
  read -rsp "PostgreSQL password: " POSTGRES_PASSWORD
  echo
  printf 'POSTGRES_PASSWORD=%s\n' "$POSTGRES_PASSWORD" >> .env
  chmod 0600 .env
  unset POSTGRES_PASSWORD
fi
docker compose -f docker-compose.yml -f docker-compose.postgres.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build
```

Do not publish the loopback HTTP endpoint to the internet. Terminate HTTPS at a
host-level reverse proxy and keep the host firewall restricted. See the
[deployment guide](docs/deployment/VPS_DEPLOYMENT_GUIDE.md) and
[security guide](docs/security/SECURITY.md).

### VPS Deployment (Ubuntu)

Keep the repository layout intact under `/opt/batobuzz`; the Docker build relies
on root `Directory.Build.props`, `global.json`, and `src`. Follow the
[VPS Deployment Guide](docs/deployment/VPS_DEPLOYMENT_GUIDE.md) for directory
ownership, HTTPS, verification, backup, and PostgreSQL instructions.

## Documentation

- [Architecture](docs/architecture/ARCHITECTURE.md)
- [VPS Deployment Guide](docs/deployment/VPS_DEPLOYMENT_GUIDE.md)
- [User Guide](docs/user-guide/QUICK_START.md)
- [Security](docs/security/SECURITY.md)
- [Nepal Accounting Rules](docs/nepal/NEPAL_ACCOUNTING_RULES.md)
- [Production Readiness Record](docs/PRODUCTION_READINESS.md)

## License

Copyright (c) 2026 BatoBuzz Technologies Pvt Ltd. All rights reserved.
