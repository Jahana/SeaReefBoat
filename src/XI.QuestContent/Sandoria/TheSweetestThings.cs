// The Sweetest Things (Log ID 0, Quest ID 8), a line-for-line port of scripts/quests/sandoria/The_Sweetest_Things.lua
// (quest rework B3b, San d'Oria group 9). Raimbroy : !pos -141 -3 34.6 230. Offered at San d'Oria fame level 2: Raimbroy's
// 532 (Prog 1), 533 (option 0: begun; any other: Prog 2), then 534 (option 0: begun). Accepted: his 536; exactly five
// Pots of Honey play 535 (400 x GIL_RATE as its argument), anything else 522. 535's finish completes it (fame +10,
// Apiarist), takes the trade, adds the gil silently and sets the quest's local var firstComplete; then, the quest now
// completed, the completed state's finish of the same 535 runs too and clears the latch. Completed: his 537; the honey
// again plays 535, whose finish (firstComplete 0) takes it, adds the title, the gil and fame +10 (repeatable). No
// outside reference.
// Kept as the Lua file has it: the repeat reward is given by hand in the completed state's finish; the latch is a local
// var (lost on zoning); 522 answers a trade of anything else, in both states.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheSweetestThings : QuestScript
    {
        public TheSweetestThings() : base(QuestLog.Sandoria, "THE_SWEETEST_THINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Sweetest_Things";

        // 400 * xi.settings.main.GIL_RATE: the event argument and both addGil. The Lua multiplies the rate as the Lua
        // number it is, and the engine reads the product with sol's integral getter (startEvent's params and addGil's
        // amount: LuaState.ToIntegral, llround), a fraction rounded half away from zero: GIL_RATE 1.5 pays 600, 1.03125
        // (412.5) pays 413. So the rate is read with its fraction (MainNumber) and the product rounded the same way;
        // Settings.Main would round the rate first (1.5 reads 2: 800). #gil_rate_decimal and #gil_rate_half pin both
        static int Gil(IQuestContext q) =>
            (int)Math.Round(400 * q.Settings.MainNumber("GIL_RATE"), MidpointRounding.AwayFromZero);

        static QuestReply RaimbroyOnTrade(IQuestContext q)
        {
            if (q.Trade.HasExactly((Item.PotOfHoney, 5)))
            {
                return QuestReply.ProgressEvent(535, Gil(q));
            }
            else
            {
                return QuestReply.Event(522);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Sandoria, Title = Title.Apiarist });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Raimbroy).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(532);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(533);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(534);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 532, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 533, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 534, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Raimbroy)
                    .OnTrade(RaimbroyOnTrade)
                    .OnTrigger(QuestReply.Event(536))
                .OnEventFinish(Zone.SouthernSanDoria, 535, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        // Note: Gil is mentioned in the event, so add silently here. Also, we need to implement a latch,
                        // since both event finishes will trigger after the complete.

                        q.Player.AddGil(Gil(q));
                        q.Quest.SetLocalVar("firstComplete", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Raimbroy)
                    .OnTrade(RaimbroyOnTrade)
                    .OnTrigger(QuestReply.Event(537))
                .OnEventFinish(Zone.SouthernSanDoria, 535, q =>
                {
                    if (q.Quest.LocalVar("firstComplete") == 0)
                    {
                        q.Trade.Confirm();

                        q.Player.AddTitle(Title.Apiarist);
                        q.Player.AddGil(Gil(q));
                        q.Player.AddFame(FameArea.Sandoria, 10);
                    }
                    else
                    {
                        q.Quest.SetLocalVar("firstComplete", 0);
                    }
                });
        }
    }
}
