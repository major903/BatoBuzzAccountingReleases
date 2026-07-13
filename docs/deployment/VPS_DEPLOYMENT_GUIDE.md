# VPS Deployment Guide

## Deployment model

The Compose default is intentionally safe for staging: SQLite is enabled, the
API has no host port, and nginx binds only to `127.0.0.1:8080`. A public server
must terminate HTTPS at a host-level reverse proxy that forwards to this
loopback endpoint. Do not expose port 8080 through the firewall.

PostgreSQL is a separate override and is never started by the default command.
It remains unsuitable for customer data until schema upgrades and live-server
recovery have been validated.

## Prerequisites

- A supported Ubuntu LTS release with security updates enabled
- Root or sudo access and a named deployment account
- Docker Engine and the Docker Compose plugin installed from Docker's official
  repository
- `sqlite3`, `gzip`, `rsync`, `curl`, and `openssl`
- A domain name and trusted TLS certificate for public access
- An external encrypted backup destination

Verify the tools before copying the application:

```bash
docker --version
docker compose version
sqlite3 --version
```

## Copy the repository

Keep the repository layout intact. The Docker build needs root
`Directory.Build.props`, `global.json`, `src`, and `deployment/docker`.

```bash
sudo install -d -m 0750 /opt/batobuzz
sudo rsync -a \
  --exclude .git \
  --exclude publish \
  --exclude dist \
  --exclude deployment/docker/data \
  --exclude deployment/docker/logs \
  /path/to/BatoBuzz/ /opt/batobuzz/

# Create the persistent API signing secret only on the first deployment.
if ! sudo test -s /opt/batobuzz/deployment/docker/.env; then
  printf 'API_SIGNING_KEY=%s\n' "$(openssl rand -hex 48)" \
    | sudo tee /opt/batobuzz/deployment/docker/.env >/dev/null
  sudo chown "$USER" /opt/batobuzz/deployment/docker/.env
  sudo chmod 0600 /opt/batobuzz/deployment/docker/.env
fi


sudo bash /opt/batobuzz/deployment/scripts/setup.sh
```

The setup script does not install Docker or change the operating system. It
checks prerequisites, validates the default Compose file, and creates the data,
log, and backup directories. Container UID/GID `10001` owns the writable API
directories.

## Start the SQLite deployment

```bash
cd /opt/batobuzz/deployment/docker
docker compose config --quiet
docker compose up -d --build
docker compose ps
curl --fail --show-error http://127.0.0.1:8080/health
```

Inspect service logs without relying on fixed container names:

```bash
docker compose logs --tail 200 api nginx
docker compose logs --follow api nginx
```

The SQLite database and API logs are stored at:

```text
/opt/batobuzz/deployment/docker/data/BatoBuzz.db
/opt/batobuzz/deployment/docker/logs/
```

## Configure public HTTPS

Keep Compose bound to loopback. Configure the host's nginx, Caddy, or another
maintained reverse proxy to:

1. Listen on ports 80 and 443 for the real domain.
2. Redirect HTTP to HTTPS.
3. Use an automatically renewed trusted certificate.
4. Proxy HTTPS requests to `http://127.0.0.1:8080`.
5. Preserve `Host`, `X-Forwarded-For`, and `X-Forwarded-Proto` headers.
6. Allow only SSH, HTTP, and HTTPS through the firewall; never expose the API
   container or PostgreSQL port.

After TLS is configured, test the public `/health` endpoint and an authenticated
API operation. Monitor certificate renewal rather than treating initial
issuance as complete.

## Opt in to PostgreSQL

Store a long unique password in the protected Compose `.env` file without
placing it in shell history or the repository. The override fails configuration
if the password is absent. This example creates a random password only when one
does not already exist:

```bash
cd /opt/batobuzz/deployment/docker
if ! grep -q '^POSTGRES_PASSWORD=' .env; then
  printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 32)" >> .env
  chmod 0600 .env
fi

docker compose \
  -f docker-compose.yml \
  -f docker-compose.postgres.yml \
  config --quiet

docker compose \
  -f docker-compose.yml \
  -f docker-compose.postgres.yml \
  up -d --build
```

Use the same two `-f` arguments for subsequent PostgreSQL lifecycle commands.
Docker administrators can inspect container environment, so restrict Docker
access to trusted operators and rotate the password after any suspected leak.

## SQLite backup and restore readiness

Create an online, integrity-checked SQLite backup:

```bash
sudo bash /opt/batobuzz/deployment/scripts/backup.sh
sudo ls -l /opt/batobuzz/backups
```

The local script retains 30 days by default. A cron entry can invoke it through
`bash` and capture failures, for example in `/etc/cron.d/batobuzz`:

```text
0 2 * * * root /bin/bash /opt/batobuzz/deployment/scripts/backup.sh >>/var/log/batobuzz-backup.log 2>&1
```

Copy completed archives to encrypted off-host storage and alert on missed or
failed jobs. Regularly restore a copy on a non-production host and reconcile
the Trial Balance, receivables/payables, and stock reports.

PostgreSQL requires `pg_dump`/`pg_restore` automation and a separately tested
recovery procedure. The SQLite backup script must not be used for PostgreSQL.

## Updating

1. Announce maintenance and stop transaction entry.
2. Create and verify a database backup.
3. Copy a reviewed, tagged source revision while preserving `data`, `logs`, and
   `backups`.
4. Run `docker compose build --pull` and review failures before restarting.
5. Run `docker compose up -d`, wait for healthy status, and inspect logs.
6. Reconcile key accounting reports before reopening access.

Retain the previous source revision and image plus the pre-update database
backup until validation is complete. Database rollback must be planned
separately from container rollback.

## Troubleshooting

- **Compose cannot create or write the database:** rerun `setup.sh` with sudo
  and confirm `data` and `logs` are owned by UID/GID 10001.
- **nginx returns 502:** check `docker compose ps` and API health/logs.
- **The host cannot bind 8080:** set a different loopback `HTTP_PORT`, for
  example `HTTP_PORT=8081 docker compose up -d`.
- **PostgreSQL interpolation fails:** export `POSTGRES_PASSWORD` and include both
  Compose files.
- **A backup job succeeds but no archive appears:** treat it as failed, inspect
  `/var/log/batobuzz-backup.log`, and verify `sqlite3`, disk space, and paths.
