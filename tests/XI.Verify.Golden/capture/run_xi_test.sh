#!/bin/sh
# Runs the C++ xi_test.exe against the docker MariaDB (127.0.0.1:3307, database xidb_golden), never the host 3306.
# usage: run_xi_test.sh <server dir> [xi_test args...]
set -e
SERVER="$1"; shift
export XI_NETWORK_SQL_HOST=127.0.0.1
export XI_NETWORK_SQL_PORT=3307
export XI_NETWORK_SQL_DATABASE=xidb_golden
export XI_NETWORK_SQL_LOGIN=root
export XI_NETWORK_SQL_PASSWORD=root
case "$XI_NETWORK_SQL_PORT" in 3307) ;; *) echo "refusing: SQL port must be 3307" >&2; exit 2 ;; esac
cd "$SERVER"
exec ./xi_test.exe "$@"
