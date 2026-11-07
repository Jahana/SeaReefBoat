// Sorcery of the North (Log ID 0, Quest ID 83), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Eperdur.lua and scripts/zones/FeiYin/npcs/Treasure_Chest.lua (quest rework B3b,
// San d'Oria group 12; call-only: both scripts now call this quest). Eperdur : !pos 129 -6 96 231. Healing the Land
// completed and this quest available: 684 while needToZone is set (Healing the Land's completion sets it; a zone other
// than Northern San d'Oria clears it), 685 once it is clear (option 0: begun). Accepted: 686 without the Fei'Yin Magic
// Tome, 687 with it (finish: the Scroll of Teleport-Vahzl and, when it fits, the tome taken, fame +40, completed by the
// raw player:completeQuest, which keeps the quest's own Quest[0][83] vars).
// Fei'Yin's Treasure Chest, accepted and no tome held: its trade is xi.treasure.onTrade with the tome as the reward
// (bypass type 2; the trade's checks, the outcome draw and the delay are xi.treasure's). Eperdur's script calls this quest
// after Healing the Land's nil (its branches come first in his script), and plays nothing of its own after this one's
// (his DefaultActions 678 is the lookup's, every other talk). The chest's script keeps its own trade when the quest does
// not act (xi.treasure.onTrade(..., 0, 0) and the expeditionary force's onChestOpen), and its trigger.
// Kept as the Lua has it: 685's finish adds the quest on option 0 in any status (a completed quest: the log's current
// bit, still completed); 687's finish reads no status; Eperdur's 686 / 687 read only this quest's status and the tome.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SorceryOfTheNorth : QuestScript
    {
        public SorceryOfTheNorth() : base(QuestLog.Sandoria, "SORCERY_OF_THE_NORTH") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Eperdur.lua onTrigger, after Healing the Land's branches, in its order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "HEALING_THE_LAND") == QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).Called().OnTrigger(q =>
                {
                    if (q.Player.NeedToZone)
                    {
                        return QuestReply.Event(684); // the standard dialog after Healing the Land
                    }
                    else
                    {
                        return QuestReply.Event(685); // start the quest
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).Called().OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.FeiyinMagicTome))
                    {
                        return QuestReply.Event(686); // during the quest
                    }
                    else
                    {
                        return QuestReply.Event(687); // finish the quest
                    }
                });

            // FeiYin Treasure_Chest.lua onTrade
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.FeiyinMagicTome))
                .Npc(Zone.Feiyin, Npc.Feiyin.TreasureChest).Called().OnTrade(q =>
                {
                    q.Trade.TreasureTrade(2, KeyItem.FeiyinMagicTome);

                    return QuestReply.NoAction();
                });

            // Eperdur.lua onEventFinish (685, 687)
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 685, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 687, q =>
                {
                    if (q.Player.GiveItem(Item.ScrollOfTeleportVahzl))
                    {
                        q.Player.DelKeyItem(KeyItem.FeiyinMagicTome);
                        q.Player.AddFame(FameArea.Sandoria, 40);
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][83] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "SORCERY_OF_THE_NORTH");
                    }
                });
        }
    }
}
