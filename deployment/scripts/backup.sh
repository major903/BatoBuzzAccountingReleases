#!/usr/bin/env bash
# Online SQLite backup with integrity validation and bounded local retention.

set -euo pipefail
umask 077

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_ROOT=$(CDPATH= cd -- "${SCRIPT_DIR}/../.." && pwd)
BACKUP_DIR=${BACKUP_DIR:-"${PROJECT_ROOT}/backups"}
DB_PATH=${DB_PATH:-"${PROJECT_ROOT}/deployment/docker/data/BatoBuzz.db"}
RETENTION_DAYS=${RETENTION_DAYS:-30}

if ! [[ ${RETENTION_DAYS} =~ ^[0-9]+$ ]]; then
    echo "RETENTION_DAYS must be a non-negative integer." >&2
    exit 1
fi

for command_name in sqlite3 gzip; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Required command not found: ${command_name}" >&2
        exit 1
    fi
done

if [[ ! -f "${DB_PATH}" ]]; then
    echo "SQLite database not found: ${DB_PATH}" >&2
    exit 1
fi

mkdir -p "${BACKUP_DIR}"
chmod 0700 "${BACKUP_DIR}"

timestamp=$(date -u +%Y%m%dT%H%M%SZ)
temporary_path="${BACKUP_DIR}/.BatoBuzz_${timestamp}.db"
archive_path="${BACKUP_DIR}/BatoBuzz_${timestamp}.db.gz"

cleanup() {
    rm -f -- "${temporary_path}" "${temporary_path}.gz"
}
trap cleanup EXIT

sqlite3 -cmd ".timeout 10000" "${DB_PATH}" ".backup \"${temporary_path}\""
integrity_result=$(sqlite3 "${temporary_path}" "PRAGMA quick_check;")
if [[ ${integrity_result} != "ok" ]]; then
    echo "Backup integrity check failed: ${integrity_result}" >&2
    exit 1
fi

gzip -9 -n "${temporary_path}"
mv -- "${temporary_path}.gz" "${archive_path}"
chmod 0600 "${archive_path}"
trap - EXIT

find "${BACKUP_DIR}" -type f -name "BatoBuzz_*.db.gz" -mtime "+${RETENTION_DAYS}" -delete
echo "Verified backup created: ${archive_path}"
