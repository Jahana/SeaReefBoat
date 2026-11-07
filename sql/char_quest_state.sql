--
-- Table structure for table `char_quest_state`
-- (the C# server's quest rows, one per quest a char has touched: XI.Quests.QuestStateSchema; map.QUEST_STATE_ROWS)
--

DROP TABLE IF EXISTS `char_quest_state`;
CREATE TABLE IF NOT EXISTS `char_quest_state` (
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
