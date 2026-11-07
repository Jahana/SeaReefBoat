// The Tigress Stirs (Log ID 7, Quest ID 17), a line-for-line port of
// scripts/quests/crystalWar/WOTG_WIN_1_The_Tigress_Stirs.lua (quest rework B3b Crystal War group 2). Windurst Waters
// [S]: Dhea Prandoleh !pos 1 -1 15, Door:Acolyte Hostel (down) !pos 124 -3 222. West Sarutabaruta [S]: qm4 !pos 150 -39
// 331.
// Available, with one nation opener completed (xi.wotg.helpers.hasCompletedFirstQuest: Steamed Rams, Snake on the Plains
// or The Fighting Fourth): Dhea Prandoleh 128, whose finish begins it. Accepted: qm4 without the Small Starfruit: the
// key item (a KeyItem reply); Dhea Prandoleh 131 (an Event); the Acolyte Hostel door with the starfruit: 129, whose
// finish completes it (a Hi-Elixir) and takes the starfruit.
// Kept as the Lua has it: 128 begins on any option; qm4 answers nil once the starfruit is held; the door answers nil
// without it (its script's message plays); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheTigressStirs : QuestScript
    {
        public TheTigressStirs() : base(QuestLog.CrystalWar, "THE_TIGRESS_STIRS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_WIN_1_The_Tigress_Stirs";

        // xi.wotg.helpers.hasCompletedFirstQuest(player) (scripts/missions/wotg/helpers.lua:27-31)
        internal static bool HasCompletedFirstQuest(IQuestPlayerView player) =>
            player.HasCompletedQuest(QuestLog.CrystalWar, "STEAMED_RAMS") ||
            player.HasCompletedQuest(QuestLog.CrystalWar, "SNAKE_ON_THE_PLAINS") ||
            player.HasCompletedQuest(QuestLog.CrystalWar, "THE_FIGHTING_FOURTH");

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.HiElixir,
            });

            b.State(QuestStatus.Available, when: q => HasCompletedFirstQuest(q.Player))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DheaPrandoleh).OnTrigger(QuestReply.ProgressEvent(128))
                .OnEventFinish(Zone.WindurstWatersS, 128, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.qm4).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SmallStarfruit))
                    {
                        return QuestReply.KeyItem(KeyItem.SmallStarfruit);
                    }
                    return null;
                })
                // In-progress reminder
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DheaPrandoleh).OnTrigger(QuestReply.Event(131))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorAcolyteHosteldown).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SmallStarfruit))
                    {
                        return QuestReply.ProgressEvent(129);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WindurstWatersS, 129, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SmallStarfruit);
                    }
                });
        }
    }
}
