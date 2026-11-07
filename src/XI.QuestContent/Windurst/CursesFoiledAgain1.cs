// Curses, Foiled Again! (1) (Log ID 2, Quest ID 32), a line-for-line port of
// scripts/quests/windurst/Curses_Foiled_Again_1.lua (quest rework B3b). Shantotto : !pos 122 -2 112 239.
// Shantotto offers it (171, option 3: begun); a Pinch of Bomb Ash and two Bone Chips complete it (173: fame +20, a Brass
// Rod), set Curses, Foiled Again!? (2)'s Timer to the next Vana'diel day and this quest's mustZone, the two waits the
// next quest's offer needs. Hiwon-Biwon's script reads this quest's status for Making Headlines.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CursesFoiledAgain1 : QuestScript
    {
        public CursesFoiledAgain1() : base(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Curses_Foiled_Again_1";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.BrassRod });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                    .OnTrigger(QuestReply.Event(171, 0, 0, 0, 0, 0, 0, Item.PinchOfBombAsh, Item.BoneChip))
                .OnEventFinish(Zone.WindurstWalls, 171, q =>
                {
                    if (q.Option == 3)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.PinchOfBombAsh, 1), (Item.BoneChip, 2)))
                        {
                            return QuestReply.ProgressEvent(173, 0, 0, 0, 0, 0, 0, Item.PinchOfBombAsh, Item.BoneChip);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.Event(172, 0, 0, 0, 0, 0, 0, Item.PinchOfBombAsh, Item.BoneChip))
                .OnEventFinish(Zone.WindurstWalls, 173, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        q.Player.SetQuestVar(QuestLog.Windurst, "CURSES_FOILED_AGAIN_2", "Timer", q.Player.VanadielUniqueDay + 1);

                        // NOTE: There's two zoning mechanics required prior to the next quest being displayed in logs.  To make this easier,
                        // setting a mustZone value for this quest as a requisite in order to utilize mustZone in the next quest's available
                        // block.
                        q.Player.SetQuestMustZone(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1");
                    }
                });
        }
    }
}
