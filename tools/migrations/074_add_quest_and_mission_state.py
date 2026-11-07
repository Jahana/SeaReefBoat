import mariadb


def migration_name():
    return "Adding char_quest_state and char_mission_state (the C# server's quest and mission rows)"


def check_preconditions(cur):
    return


def needs_to_run(cur):
    for table in ("char_quest_state", "char_mission_state"):
        cur.execute("SHOW TABLES LIKE '" + table + "'")
        if not cur.fetchone():
            return True
    return False


def migrate(cur, db):
    try:
        cur.execute(
            """CREATE TABLE IF NOT EXISTS `char_quest_state` (
  `charid`      int(10) unsigned NOT NULL,
  `quest_key`   varchar(96) NOT NULL,
  `area`        tinyint(3) unsigned NOT NULL,
  `quest_id`    smallint(5) unsigned NOT NULL,
  `status`      tinyint(3) unsigned NOT NULL DEFAULT 0,
  `step`        int(11) NOT NULL DEFAULT 0,
  `progress`    json DEFAULT NULL,
  `repeat_by`   bigint(20) NOT NULL DEFAULT 0,
  `updated_at`  timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`, `quest_key`),
  UNIQUE KEY `uq_char_quest_wire` (`charid`, `area`, `quest_id`),
  KEY `idx_char_quest_area` (`area`, `quest_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"""
        )
        cur.execute(
            """CREATE TABLE IF NOT EXISTS `char_mission_state` (
  `charid`         int(10) unsigned NOT NULL,
  `area`           tinyint(3) unsigned NOT NULL,
  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,
  `status`         int(10) unsigned NOT NULL DEFAULT 0,
  `completed_mask` bigint(20) unsigned NOT NULL DEFAULT 0,
  `progress`       json DEFAULT NULL,
  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`, `area`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"""
        )
        db.commit()
    except mariadb.Error as err:
        print("Something went wrong: {}".format(err))
