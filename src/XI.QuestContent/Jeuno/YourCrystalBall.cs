// Your Crystal Ball (Log ID 3, Quest ID 9), a line-for-line port of scripts/quests/jeuno/Your_Crystal_Ball.lua (quest
// rework B3b). Kurou-Morou : !pos -4 -6 -28 245; Rockwell : !pos -18 -13 181 198. From Jeuno fame level 2 Kurou-Morou
// offers 194 (option 0: begun). Accepted: he reminds (191); an Ahriman Lens (tradeMatches) at the Rockwell pool of the
// Maze of Shakhrami at Prog 0 is taken (Prog 1, Wait = now + 60, SUBMERGED_ITEM), at Prog 1 gets MORE_THAN_ONE; the
// pool's trigger at Prog 1 says WAIT_A_BIT_LONGER until Wait, then plays 52 (a free slot: Prog 2, the Divination
// Sphere; none: ITEM_CANNOT_BE_OBTAINED). The sphere (tradeMatches) to Kurou-Morou at Prog 2 plays 196: completed
// (Fortune-Teller in Training), fame +16 x3, the sphere taken, his FORTUNE_TOLD_BY_KUROU_MOROU line. Kept: 52 checks a
// free slot by hand (getFreeSlotsCount() ~= 0) and adds the sphere with addItem, with no obtained message.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class YourCrystalBall : QuestScript
    {
        public YourCrystalBall() : base(QuestLog.Jeuno, "YOUR_CRYSTAL_BALL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Your_Crystal_Ball";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.FortuneTellerInTraining });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 2)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(QuestReply.ProgressEvent(194))
                .OnEventFinish(Zone.LowerJeuno, 194, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    return QuestReply.Event(191); // Reminder
                })
                .OnTrade(q =>
                {
                    if (q.Quest.Step == 2 && q.Trade.Matches((Item.DivinationSphere, 1)))
                    {
                        return QuestReply.ProgressEvent(196);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 196, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 16);
                        q.Player.AddFame(FameArea.Bastok, 16);
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Trade.Complete();
                        // player:messageText(npc, lowerJeunoID.text.FORTUNE_TOLD_BY_KUROU_MOROU, false, 6)
                        q.Text.MessageText(Text.LowerJeuno.FORTUNE_TOLD_BY_KUROU_MOROU, false, 6);
                    }
                })
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.Rockwell).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        if (q.Player.SystemTime >= q.Quest.Var("Wait"))
                        {
                            return QuestReply.ProgressEvent(52);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.MazeOfShakhrami.WAIT_A_BIT_LONGER, 0, Item.DivinationSphere);
                        }
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.AhrimanLens, 1)))
                    {
                        int progress = q.Quest.Step;
                        if (progress == 0)
                        {
                            q.Trade.Complete();
                            q.Quest.SetStep(1);
                            q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time
                            return QuestReply.MessageSpecial(Text.MazeOfShakhrami.SUBMERGED_ITEM, Item.AhrimanLens);
                        }
                        else if (progress == 1)
                        {
                            return QuestReply.MessageSpecial(Text.MazeOfShakhrami.MORE_THAN_ONE, Item.AhrimanLens);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.MazeOfShakhrami, 52, q =>
                {
                    if (q.Player.FreeSlotCount != 0)
                    {
                        q.Quest.SetStep(2);
                        q.Player.AddItem(Item.DivinationSphere);
                    }
                    else
                    {
                        q.Text.Special(Text.MazeOfShakhrami.ITEM_CANNOT_BE_OBTAINED, Item.DivinationSphere);
                    }
                });
        }
    }
}
