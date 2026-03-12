#!/usr/bin/env bash
set -euo pipefail

DB="gasoholic.db"

if [ ! -f "$DB" ]; then
  echo "No database found — nothing to reset."
  exit 0
fi

read -rp "Delete $DB and all data? [y/N] " confirm
if [[ "${confirm,,}" != "y" ]]; then
  echo "Aborted."
  exit 0
fi

rm -f "$DB" "$DB-wal" "$DB-shm"
echo "Done. The database will be recreated on next 'dotnet run'."
