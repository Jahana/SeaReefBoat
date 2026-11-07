--
-- Table structure for table `char_mission_state`
-- (the C# server's mission rows, one per mission log: XI.Quests.MissionStateSchema; map.QUEST_STATE_ROWS)
--

DROP TABLE IF EXISTS `char_mission_state`;
CREATE TABLE IF NOT EXISTS `char_mission_state` (
  `charid`         int(10) unsigned NOT NULL,
  `area`           tinyint(3) unsigned NOT NULL,
  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,
  `status`         int(10) unsigned NOT NULL DEFAULT 0,
  `completed_mask` bigint(20) unsigned NOT NULL DEFAULT 0,
  `progress`       json DEFAULT NULL,
  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`, `area`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
