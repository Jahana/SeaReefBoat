#!/bin/sh
# Builds the C++ xi_test.exe with the golden capture hooks (golden_hooks.*, xi_test_hooks.patch) and leaves the server
# tree as it was: the patch is applied to <server>/src/test, only the xi_test target is built (RelWithDebInfo, the
# Ninja Multi-Config tree in <server>/build, MSVC's vcvars64 environment), the exe is copied to <server>/xi_test.exe
# (where run_xi_test.sh runs it) and the patch is reverted again, also when the build fails.
#
# usage: sh build_xi_test.sh [server dir]      (default: H:/023-FFXI-server/server)
#        XI_VS_DIR=<Visual Studio dir>          (default: H:/Microsoft Visual Studio/18/Community)
#        XI_CMAKE=<cmake.exe>                   (default: the one the build tree was configured with)
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
SERVER=${1:-/h/023-FFXI-server/server}
PATCH="$HERE/xi_test_hooks.patch"
VS=${XI_VS_DIR:-"H:/Microsoft Visual Studio/18/Community"}
CMAKE=${XI_CMAKE:-$(sed -n 's/^CMAKE_COMMAND:INTERNAL=//p' "$SERVER/build/CMakeCache.txt")}

if [ -n "$(git -C "$SERVER" status --short --untracked-files=no)" ]; then
  echo "refusing: $SERVER has local changes" >&2
  exit 2
fi

git -C "$SERVER" apply --check "$PATCH"
git -C "$SERVER" apply "$PATCH"
TMP=$(mktemp -d)
trap 'git -C "$SERVER" apply -R "$PATCH"; rm -rf "$TMP"' EXIT
trap 'exit 1' INT TERM

winpath() { cygpath -w "$1" 2>/dev/null || echo "$1"; }

cat > "$TMP/build.bat" <<EOF
@echo off
call "$(winpath "$VS/VC/Auxiliary/Build/vcvars64.bat")" >nul || exit /b 1
cd /d "$(winpath "$SERVER")" || exit /b 1
"$(winpath "$CMAKE")" --build build --config RelWithDebInfo --target xi_test || exit /b 1
EOF

cmd //c "$(winpath "$TMP/build.bat")"
cp "$SERVER/build/src/test/RelWithDebInfo/xi_test.exe" "$SERVER/xi_test.exe"
echo "built $SERVER/xi_test.exe"
