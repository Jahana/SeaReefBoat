#!/bin/sh
# usage: sh tools/new_worktree.sh <name>   (from the csharp folder)
# Makes the agent worktree ../wt/<name> on a new branch <name> from HEAD. git leaves out the meshes and the TLS keys
# (.gitignore), so the worktree gets hard links to this folder's navmeshes/ and ximeshes/ files (no copy, and
# deleting the worktree leaves these intact; a junction would not be safe under rm -rf) and copies of the keys.
# That way the tests that find their root above their bin folder see a complete server folder.
set -e
name="$1"; [ -n "$name" ] || { echo "usage: new_worktree.sh <name>" >&2; exit 2; }
wt="../wt/$name"
git worktree add -q "$wt" -b "$name"
for d in navmeshes ximeshes; do cp -rl "$d" "$wt/$d"; done
for f in login.cert login.key profile.cert profile.key; do [ -f "$f" ] && cp "$f" "$wt/"; done
echo "$wt"
