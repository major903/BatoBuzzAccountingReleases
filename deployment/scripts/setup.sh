#!/usr/bin/env bash
# Prepare an already-copied BatoBuzz repository for Docker Compose deployment.
# Docker installation is intentionally left to the operating-system operator.

set -euo pipefail

if [[ ${EUID} -ne 0 ]]; then
    echo "Run this preparation script with sudo or as root." >&2
    exit 1
fi

for command_name in docker sqlite3 gzip; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command not found: ${command_name}" >&2
        exit 1
    fi
done

if ! docker compose version >/dev/null 2>&1; then
    echo "The Docker Compose plugin is required." >&2
    exit 1
fi

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_ROOT=$(CDPATH= cd -- "${SCRIPT_DIR}/../.." && pwd)
COMPOSE_DIR="${PROJECT_ROOT}/deployment/docker"

if [[ ! -f "${PROJECT_ROOT}/Directory.Build.props" || ! -d "${PROJECT_ROOT}/src" ]]; then
    echo "Keep the full repository layout under ${PROJECT_ROOT}; required build inputs are missing." >&2
    exit 1
fi

# The API image runs with UID/GID 10001.
install -d -o 10001 -g 10001 -m 0750 "${COMPOSE_DIR}/data" "${COMPOSE_DIR}/logs"
install -d -o root -g root -m 0700 "${PROJECT_ROOT}/backups"

cd "${COMPOSE_DIR}"
docker compose config --quiet

echo "Deployment directories are ready."
echo "Start SQLite mode with: cd ${COMPOSE_DIR} && docker compose up -d --build"
