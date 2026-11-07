#!/bin/sh
# usage: sh tools/golden_lane_db_check.sh   (from the csharp folder; no container needed)
#
# Checks tools/golden_lane_db.sh's refusals: every name that is not a lane's (xidb_lane<suffix>) exits 2 before a docker
# call. A docker on the PATH that only records its call stands in for the real one, so nothing is dropped if a refusal
# is missing.
dir=$(mktemp -d)
trap 'rm -rf "$dir"' EXIT
cat > "$dir/docker" <<'EOF'
#!/bin/sh
echo "$*" >> "$(dirname "$0")/calls"
exit 99
EOF
chmod +x "$dir/docker"
fails=0
for name in xidb mysql sys information_schema performance_schema xidb_bots xidb_golden xidb_lane- 'xidb_lane2;x' \
            xidb_lane_0123456789012345678901234567890123456789 ""; do
  rm -f "$dir/calls"
  PATH="$dir:$PATH" sh tools/golden_lane_db.sh "$name" 2>/dev/null
  code=$?
  if [ "$code" = 2 ] && [ ! -e "$dir/calls" ]; then
    echo "ok    refused '$name'"
  else
    echo "FAIL  '$name': exit $code$([ -e "$dir/calls" ] && echo ', docker called')"
    fails=$((fails + 1))
  fi
done
# a lane's name passes the check: it reaches docker (the stand-in, exit 99 -> the script's exit 3)
rm -f "$dir/calls"
PATH="$dir:$PATH" sh tools/golden_lane_db.sh xidb_lane2 2>/dev/null
code=$?
if [ "$code" = 3 ] && [ -e "$dir/calls" ]; then
  echo "ok    xidb_lane2 reaches docker"
else
  echo "FAIL  xidb_lane2: exit $code"
  fails=$((fails + 1))
fi
echo "golden_lane_db_check: $fails failed"
[ "$fails" = 0 ]
