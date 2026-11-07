--
-- Table structure for table `char_assault_state`
-- (the C# server's assault row, one per char: XI.Quests.AssaultStateSchema; map.QUEST_STATE_ROWS)
--

DROP TABLE IF EXISTS `char_assault_state`;
CREATE TABLE IF NOT EXISTS `char_assault_state` (
  `charid`         int(10) unsigned NOT NULL,
  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,
  `completed_mask` varbinary(16) NOT NULL DEFAULT '',
  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
