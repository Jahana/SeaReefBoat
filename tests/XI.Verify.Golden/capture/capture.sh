#!/bin/sh
# Regenerates golden/combat.tsv from the real C++ server (xi_test.exe), against the docker MariaDB only.
# The xi_test.exe must carry the golden_hooks (build it with build_xi_test.sh, which applies xi_test_hooks.patch to
# the server's src/test for the build only).
#
# usage: sh capture.sh [server dir]      (default: H:/023-FFXI-server/server)
#        XI_GOLDEN_ONLY=<Lua pattern> sh capture.sh   runs only the matching cases into golden/partial.tsv
#
# 1. clones xidb into xidb_golden inside the docker container xi-live-db (127.0.0.1:3307) when it is missing,
# 2. copies golden_capture_spec.lua into <server>/scripts/tests/golden_capture/ (untracked) for the run,
# 3. runs xi_test.exe --file golden_capture with XI_NETWORK_SQL_* pointing at 127.0.0.1:3307/xidb_golden,
# 4. removes the spec again and writes golden/combat.tsv.
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/.." && pwd)
SERVER=${1:-/h/023-FFXI-server/server}
OUT="$ROOT/golden/combat.tsv"
[ -n "$XI_GOLDEN_ONLY" ] && OUT="$ROOT/golden/partial.tsv"

if ! docker exec xi-live-db mariadb -uroot -proot -e "USE xidb_golden" 2>/dev/null; then
  docker exec xi-live-db sh -c "mariadb -uroot -proot -e 'CREATE DATABASE xidb_golden' && mariadb-dump -uroot -proot --single-transaction --routines --triggers xidb | mariadb -uroot -proot xidb_golden"
fi

SPECDIR="$SERVER/scripts/tests/golden_capture"
mkdir -p "$SPECDIR"
cp "$HERE/golden_capture_spec.lua" "$SPECDIR/golden_capture.lua"
trap 'rm -rf "$SPECDIR"' EXIT

export XI_GOLDEN_DIR=$(cygpath -m "$ROOT" 2>/dev/null || echo "$ROOT")
export XI_GOLDEN_OUT="$XI_GOLDEN_DIR/golden/combat.tsv.tmp"
sh "$HERE/run_xi_test.sh" "$SERVER" --file golden_capture > "$ROOT/golden/capture.log" 2>&1 || { tail -30 "$ROOT/golden/capture.log"; exit 1; }
mv "$ROOT/golden/combat.tsv.tmp" "$OUT"
grep -c . "$OUT"
