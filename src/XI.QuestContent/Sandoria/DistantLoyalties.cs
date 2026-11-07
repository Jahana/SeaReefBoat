// Distant Loyalties (Log ID 0, Quest ID 74), a line-for-line port of scripts/quests/sandoria/Distant_Loyalties.lua (quest
// rework B3b, San d'Oria group 8). Femitte : !pos -17.7129 2.1000 10.1636 230; Michea : !pos -299.2114 -15.9717 -156.0629
// 235. Offered at San d'Oria fame level 4: Femitte's 663 (option 0: begun, the Goldsmithing Order). Accepted: Michea's
// 315 (the order taken, Prog 1), 316 while Prog 1; a Mythril Ingot to him at Prog 1 (317: taken, Prog 2, mustZone); 320
// until the char zones, then 318 (Prog 3, Mythril Hearts); Femitte's 665 with the hearts at Prog 3 (completed: White Cape,
// fame +40; finalCS 1, the hearts taken); Rouva's 664 throughout. Completed with finalCS: Michea's 319 (finalCS 0). No
// outside reference.
// Kept as the Lua has it: 663's option 0 accepts (any other declines); 319's finish deletes the Goldsmithing Order, long
// gone; finalCS is set after complete's cleanup and read in the completed state.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class DistantLoyalties : QuestScript
    {
        public DistantLoyalties() : base(QuestLog.Sandoria, "DISTANT_LOYALTIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Distant_Loyalties";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Sandoria, Item = Item.WhiteCape });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 4)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Femitte).OnTrigger(QuestReply.ProgressEvent(663))
                .OnEventFinish(Zone.SouthernSanDoria, 663, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.GoldsmithingOrder);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Femitte).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 3 &&
                        q.Player.HasKeyItem(KeyItem.MythrilHearts))
                    {
                        return QuestReply.ProgressEvent(665);
                    }
                    return null;
                })
                // Dialogue will repeat until the quest is completed.
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva).OnTrigger(QuestReply.Event(664))
                .OnEventFinish(Zone.SouthernSanDoria, 665, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("finalCS", 1);
                        q.Player.DelKeyItem(KeyItem.MythrilHearts);
                    }
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.MythrilIngot, 1)) &&
                            q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(317);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0 &&
                            q.Player.HasKeyItem(KeyItem.GoldsmithingOrder))
                        {
                            return QuestReply.ProgressEvent(315);
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.Event(316); // Will repeat until the player trades the Myrthil Ingot
                        }
                        else if (questProgress == 2 &&
                            q.Quest.MustZone)
                        {
                            return QuestReply.Event(320); // Will repeat until the player zones
                        }
                        else if (questProgress == 2 &&
                            !q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(318);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.BastokMarkets, 315, q =>
                {
                    q.Player.DelKeyItem(KeyItem.GoldsmithingOrder);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMarkets, 317, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(2);
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.BastokMarkets, 318, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.GiveKeyItem(KeyItem.MythrilHearts);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea).OnTrigger(q =>
                {
                    if (q.Quest.Var("finalCS") == 1)
                    {
                        return QuestReply.ProgressEvent(319);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarkets, 319, q =>
                {
                    q.Player.DelKeyItem(KeyItem.GoldsmithingOrder);
                    q.Quest.SetVar("finalCS", 0);
                });
        }
    }
}
