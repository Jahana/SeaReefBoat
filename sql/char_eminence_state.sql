--
-- Table structure for table `char_eminence_state`
-- (the C# server's Records of Eminence rows, one per record active or completed: XI.Quests.EminenceStateSchema; map.QUEST_STATE_ROWS)
--

DROP TABLE IF EXISTS `char_eminence_state`;
CREATE TABLE IF NOT EXISTS `char_eminence_state` (
  `charid`     int(10) unsigned NOT NULL,
  `record_id`  int(10) unsigned NOT NULL,
  `slot`       tinyint(3) unsigned NOT NULL DEFAULT 255,
  `progress`   int(10) unsigned NOT NULL DEFAULT 0,
  `completed`  tinyint(1) NOT NULL DEFAULT 0,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`charid`, `record_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
