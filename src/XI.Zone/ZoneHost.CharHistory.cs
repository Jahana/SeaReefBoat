using XI.Persistence;

namespace XI.Zone
{
    // CCharEntity::m_charHistory: the char's lifetime counters (char_history), read when the char enters a zone
    // (charutils::ReadHistory in CZone::IncreaseZoneCounter) and written when it leaves (charutils::WriteHistory in
    // DecreaseZoneCounter). They count where the C++ counts them: a mob's kill (DistributeRewards, its owner), a
    // char's death (CCharEntity::Die), entering the own mog house (0x00A), a party joined (CParty::AddMember), an
    // alliance formed or joined (CAlliance::addParty), the end of a cast / an ability / a weapon skill / an item use,
    // a chat line (0x0B5), an NPC talked to (0x01A), an engage (CCharEntity::OnEngage), a GM call sent (0x0D3) and
    // the ground covered (0x015). getHistory reads them.
    public sealed partial class ZoneHost
    {
        // the HISTORY_TYPE indices (xi.history)
        internal const int HistoryEnemiesDefeated = 0, HistoryTimesKnockedOut = 1, HistoryMhEntrances = 2, HistoryJoinedParties = 3,
            HistoryJoinedAlliances = 4, HistorySpellsCast = 5, HistoryAbilitiesUsed = 6, HistoryWsUsed = 7, HistoryItemsUsed = 8,
            HistoryChatsSent = 9, HistoryNpcInteractions = 10, HistoryBattlesFought = 11, HistoryGmCalls = 12,
            HistoryDistanceTravelled = 13, HistoryCount = 14;

        private static readonly string[] HistoryColumns =
        {
            "enemies_defeated", "times_knocked_out", "mh_entrances", "joined_parties", "joined_alliances", "spells_cast",
            "abilities_used", "ws_used", "items_used", "chats_sent", "npc_interactions", "battles_fought", "gm_calls",
            "distance_travelled",
        };

        internal static readonly string SqlHistory = $"SELECT {string.Join(", ", HistoryColumns)} FROM char_history WHERE charid = ? LIMIT 1";

        // charutils::ReadHistory: the row's counters; without a row they stay as they are
        private void ReadHistory(CharData c)
        {
            var r = Exec(SqlHistory, BoundValue.Of(c.Id));
            if (r == null || !r.Next()) return;
            for (int i = 0; i < HistoryCount; i++) c.History[i] = r.Get<uint>(HistoryColumns[i]);
        }

        // charutils::WriteHistory: every counter, the row made when missing
        private void WriteHistory(CharData c)
        {
            var args = new BoundValue[HistoryCount + 1];
            args[0] = BoundValue.Of(c.Id);
            for (int i = 0; i < HistoryCount; i++) args[i + 1] = BoundValue.Of(c.History[i]);
            Exec($"INSERT INTO char_history SET charid = ?, {string.Join(", ", System.Array.ConvertAll(HistoryColumns, col => col + " = ?"))} " +
                 $"ON DUPLICATE KEY UPDATE {string.Join(", ", System.Array.ConvertAll(HistoryColumns, col => $"{col} = VALUES({col})"))}", args);
        }
    }
}
