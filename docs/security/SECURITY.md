# Security Guide

## Supported deployment boundaries

The Windows desktop application is offline-first and stores each user's data on
that Windows account. The API is a separate deployment surface. Do not expose
the API directly to the internet until authentication, authorization, TLS, rate
limits, database upgrades, backups, and recovery have been validated for the
specific environment.

The default Docker Compose configuration binds nginx to `127.0.0.1:8080` and
gives the API no host port. Keep that secure default when a host-level TLS proxy
is used. Setting `BIND_ADDRESS=0.0.0.0` makes the HTTP proxy public and must not
be done without an HTTPS termination plan and a firewall.

## Local data protection

- Desktop data is stored at `%LOCALAPPDATA%\BatoBuzz\Accounting\BatoBuzz.db`.
- Diagnostic logs are below `%LOCALAPPDATA%\BatoBuzz\Accounting\logs`.
- The SQLite database is not encrypted by the application. Use Windows device
  encryption or BitLocker, strong Windows accounts, screen locking, and
  restricted filesystem permissions on production computers.
- Do not attach a customer database or diagnostic log to a public issue. Use an
  approved encrypted support channel and remove unnecessary personal or
  financial data.

## Credentials and deployment secrets

- Never commit `.env` files, databases, certificates, passwords, or production
  configuration overrides. Inspect every change before committing it.
- PostgreSQL is opt-in. Supply `POSTGRES_PASSWORD` only in the deployment
  environment when loading `docker-compose.postgres.yml`; no default password
  is provided.
- Compose requires `API_SIGNING_KEY` to contain at least 32 bytes. Generate it
  once, store it in the owner-only deployment environment, and preserve it
  across container recreation; rotating it invalidates existing API tokens.
- Protect deployment environment files with owner-only permissions. Do not
  print rendered Compose configuration into shared logs because it can contain
  secrets.
- Rotate a secret immediately if it is exposed and review application and
  database logs for unexpected access.

## Release integrity

The release scripts perform a smoke check, fail on build-tool errors, clean
their artifact directories, and emit `dist\SHA256SUMS.txt`. Checksums detect
file changes but do not prove publisher identity.

Production releases must additionally:

1. Be built by CI from a reviewed, clean, tagged commit.
2. Be Authenticode-signed with the BatoBuzz publisher certificate, including
   the installer and uninstaller.
3. Verify the signature and SHA-256 manifest on a clean Windows machine.
4. Run malware scanning and a dependency vulnerability/license review.
5. Preserve the source revision, build log, SBOM, and release manifest.

Code signing and SBOM generation are not automated by this repository and
remain release-operator responsibilities.

## Backup and recovery

- Take a verified backup before installing an update or restoring a database.
- Keep multiple generations, including an encrypted copy on a separate device
  or service.
- Test restores on a non-production machine. A backup is not valid until it can
  be opened and key reports reconcile.
- Stop posting during maintenance and retain the desktop restore safety copy
  until validation is complete.
- PostgreSQL requires a PostgreSQL-native backup and restore plan; copying its
  data directory is not a safe logical backup.

## Operational checklist

- Use a dedicated, non-administrator Windows account for daily desktop use.
- The v1.0 desktop is single-owner; do not share its owner credential. Use
  separate protected Windows accounts and databases where operator separation
  is required, or deploy a reviewed multi-user API configuration.
- Review failed logins, permissions, backups, and logs periodically.
- Restrict database and backup directories to authorized personnel.
- Apply supported Windows, .NET, WebView2, container, and database security
  updates after staging validation.
- Monitor container health and restart loops. The health endpoint is an
  availability signal, not a complete accounting-integrity check.

## Reporting a vulnerability

Report vulnerabilities privately through the support channel supplied by
BatoBuzz Technologies. Include the affected version, reproducible steps, and
impact, but do not include real credentials, databases, tax records, or personal
information. Allow the publisher time to investigate before disclosure.
