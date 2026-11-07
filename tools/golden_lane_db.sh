#!/bin/sh
# usage: sh tools/golden_lane_db.sh <name>   (from the csharp folder; needs the docker container xi-live-db running)
#
# (Re)creates a golden lane's database: <name> on the docker MariaDB xi-live-db (127.0.0.1:3307) as a full copy of
# xidb, its schema and its data, triggers, routines and events included (mariadb-dump --single-transaction | mariadb,
# both inside the container). The golden suite then runs on it with --db <name>:
#   dotnet tests/XI.Verify.QuestGolden.Live/bin/Release/net10.0/XI.Verify.QuestGolden.Live.dll replay all --jobs 2 --db <name>
# A lane is how two worktrees record and replay at once: each on its own database, with its own chars, world tables
# (server_variables, conquest_system) and char-id locks (golden-<db>-<id>). Section 5 of
# docs/superpowers/specs/2026-10-21-quest-rework-b3a-design.md ("Lanes") has the rules.
#
# - It takes only a lane's name: xidb_lane<suffix>, letters, digits and _, at most 48 (xidb_lane2, xidb_lane3). Any other
#   name is refused (exit 2) before a docker call: xidb (the live server's and the default lane's database), the server's
#   own (mysql, sys, information_schema, performance_schema), the other suites' (xidb_bots, xidb_golden). The refusals
#   have a check: sh tools/golden_lane_db_check.sh (no container needed).
# - The copy fails if mariadb-dump or mariadb fails (pipefail, in the container's bash) or if the lane has not as many
#   tables as xidb (xidb's golden_world_* not counted).
# - It is safe to re-run: the lane is dropped and made again from xidb as it is now. Re-run it after a schema change to
#   xidb (a migration, a new table): a trace records every table with a charid column, so a lane with an old schema
#   records different steps. Its world tables are xidb's at the copy, but a golden run does not read them: every world
#   starts from the snapshot tests/golden/world-tables.txt.
# - xidb is only read: --single-transaction reads the InnoDB tables at one point without locks; the Aria and MyISAM
#   tables are read as they are, without a lock either (--skip-lock-tables), so the live server never waits on the copy.
# - Nothing else uses a lane: the live server and the other suites name xidb (XI.Verify.Bots.Live its own xidb_bots,
#   the C++ capture xidb_golden), and no code lists the databases.
# - A golden run's private world tables (golden_world_<tag>_server_variables / _conquest_system, made while a run
#   holds its locks, tests/XI.Verify.QuestGolden.Live/World.cs) are not copied over: every run makes its own.
# - It prints the lane's table count, its size and how long the copy took (about 14 MB and a few seconds in 10/2026).
set -e
name="$1"
usage() { echo "usage: sh tools/golden_lane_db.sh xidb_lane<suffix>   (letters, digits and _, at most 48)" >&2; exit 2; }
[ -n "$name" ] || usage
case "$name" in
  xidb_lane*) ;;
  *) echo "golden_lane_db: refusing $name: only a lane's database (xidb_lane<suffix>) is dropped and made again" >&2; exit 2 ;;
esac
case "$name" in *[!A-Za-z0-9_]*) usage ;; esac
[ "${#name}" -le 48 ] || usage
db="docker exec -i xi-live-db mariadb -uroot -proot"
$db -e "SELECT 1" >/dev/null 2>&1 || { echo "golden_lane_db: xi-live-db does not answer (start the container)" >&2; exit 3; }
start=$(date +%s%N)
# the database's own charset and collation, as xidb has them (the tables carry theirs in the dump)
cs=$($db -N -e "SELECT CONCAT(default_character_set_name, ' COLLATE ', default_collation_name) FROM information_schema.schemata WHERE schema_name = 'xidb'")
[ -n "$cs" ] || { echo "golden_lane_db: no database xidb" >&2; exit 3; }
$db -e "DROP DATABASE IF EXISTS \`$name\`; CREATE DATABASE \`$name\` CHARACTER SET $cs"
docker exec xi-live-db bash -c "set -o pipefail; mariadb-dump -uroot -proot --single-transaction --skip-lock-tables --routines --triggers --events xidb | mariadb -uroot -proot $name"
# a golden run's private world tables (golden_world_<tag>_*, World.cs), if one was running on xidb: not the lane's
for t in $($db -N -e "SELECT table_name FROM information_schema.tables WHERE table_schema = '$name' AND table_name LIKE 'golden\_world\_%'"); do
  $db -e "DROP TABLE \`$name\`.\`$t\`"
done
# the live Unity state (chars' unity_leader, unity_system's members and points) is not the lane's: it is in every
# flow's 0x063 login packet, so a copy made after the live chars join a Unity records and replays differently from
# every other lane (TODO "Golden lane DBs inherit the live Unity state"). Every lane starts from the same state: no
# char in a Unity, leader 1 at 2 members, every count and point 0 (what the traces on main were recorded with).
$db -e "UPDATE \`$name\`.char_profile SET unity_leader = 0 WHERE unity_leader <> 0;
        UPDATE \`$name\`.unity_system SET members_current = IF(leader = 1, 2, 0), points_current = 0, members_prev = 0,
                                          points_prev = 0"
end=$(date +%s%N)
# every table of xidb, its golden_world_* aside, is in the lane
want=$($db -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'xidb' AND table_name NOT LIKE 'golden\_world\_%'")
have=$($db -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$name'")
if [ "$have" != "$want" ]; then
  echo "golden_lane_db: $name has $have tables, xidb $want (golden_world_* aside): the copy is incomplete" >&2
  exit 4
fi
$db -N -e "SELECT CONCAT('$name: ', (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$name'), ' tables (xidb ',
                         (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'xidb'), '), ',
                         (SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema = '$name'), ' triggers, ',
                         ROUND((SELECT SUM(data_length + index_length) FROM information_schema.tables WHERE table_schema = '$name') / 1048576, 1), ' MB')"
echo "$name: copied from xidb in $(( (end - start) / 1000000 )) ms"
