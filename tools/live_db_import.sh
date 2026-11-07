#!/bin/bash
# Sanctioned xidb schema load, mirroring tools/dbtool.py setup_db():
#   - import every sql/*.sql in sorted order
#   - force triggers.sql last (matches dbtool.py's index('triggers.sql') move)
#   - SET foreign_key_checks=0 per file (mysqldump headers vary; guarantees parent/child DDL is order-independent)
# Container-local paths; the host sql/ dir is copied to /tmp/sql by the caller.
set -eu
cd /tmp/sql
order=/tmp/order.txt
: > "$order"
ls *.sql | grep -v '^triggers.sql$' >> "$order" || true
[ -f triggers.sql ] && echo triggers.sql >> "$order"
n=0
while IFS= read -r f; do
  printf 'IMPORT %s\n' "$f"
  ( echo 'SET FOREIGN_KEY_CHECKS=0;'; cat "$f" ) | mariadb -uroot -proot xidb
  n=$((n+1))
done < "$order"
printf 'IMPORTED_FILES=%d\n' "$n"
mariadb -uroot -proot -N -B xidb -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='xidb';" | sed 's/^/TABLES=/'
