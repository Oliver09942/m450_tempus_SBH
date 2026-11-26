#!/bin/sh
# On first start, copy the seed database (like Start-Entwicklung.cmd), then start Tempus.
set -e
if [ ! -e /daten/tempus.sqlite3 ] && [ ! -e /daten/saldo.sqlite3 ]; then
    cp /seed/tempus.sqlite3 /daten/tempus.sqlite3
fi
exec dotnet /app/Tempus.dll --BindAny true --Port "${TEMPUS_PORT}" --DataDir /daten --NoBrowser true "$@"
