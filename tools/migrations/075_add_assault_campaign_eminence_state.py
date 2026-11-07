import os
import re

import mariadb

TABLES = ("char_assault_state", "char_campaign_state", "char_eminence_state")


def migration_name():
    return "Adding char_assault_state, char_campaign_state and char_eminence_state (the C# server's rows)"


def check_preconditions(cur):
    return


def needs_to_run(cur):
    for table in TABLES:
        cur.execute("SHOW TABLES LIKE '" + table + "'")
        if not cur.fetchone():
            return True
    return False


# dbtool's migrate does not import sql/triggers.sql, so this brings an existing database's char_delete up to date
def char_delete_trigger():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "sql", "triggers.sql")
    with open(path, encoding="utf-8") as f:
        return re.search(r"CREATE TRIGGER char_delete\b.*?\bEND\b", f.read(), re.S).group(0)


def migrate(cur, db):
    try:
        cur.execute(
            """CREATE TABLE IF NOT EXISTS `char_assault_state` (
  `charid`         int(10) unsigned NOT NULL,
  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,
  `completed_mask` varbinary(16) NOT NULL DEFAULT '',
  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"""
        )
        cur.execute(
            """CREATE TABLE IF NOT EXISTS `char_campaign_state` (
  `charid`         int(10) unsigned NOT NULL,
  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,
  `completed_mask` varbinary(64) NOT NULL DEFAULT '',
  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"""
        )
        cur.execute(
            """CREATE TABLE IF NOT EXISTS `char_eminence_state` (
  `charid`     int(10) unsigned NOT NULL,
  `record_id`  int(10) unsigned NOT NULL,
  `slot`       tinyint(3) unsigned NOT NULL DEFAULT 255,
  `progress`   int(10) unsigned NOT NULL DEFAULT 0,
  `completed`  tinyint(1) NOT NULL DEFAULT 0,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`, `record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"""
        )
        cur.execute("DROP TRIGGER IF EXISTS char_delete")
        cur.execute(char_delete_trigger())
        db.commit()
    except mariadb.Error as err:
        print("Something went wrong: {}".format(err))
