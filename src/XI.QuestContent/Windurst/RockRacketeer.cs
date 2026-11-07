// Rock Racketeer (Log ID 2, Quest ID 26), a line-for-line port of scripts/quests/windurst/Rock_Racketeer.lua (quest
// rework B3b). Nanaa Mihgo !pos 62 -4 240 241; Ardea !pos -198.231 -6 -71.379 (Bastok Markets); Varun !pos 9.989 -2.5
// -7.597 (Windurst Woods); Mythril Seam !pos 210 -32 -63 143. Offered by Nanaa Mihgo (93) once Mihgo's Amigo is
// completed, at Windurst fame level 3, while Mihgo's Amigo's mustZone is clear; option 1 begins it (the Sharp Gray
// Stone). Accepted: Nanaa Mihgo's 94 / 95 (Prog 0, Option 0 / 1), 98 (Prog 1: Prog 2), 99 (Prog 2); Bopa Greso's 96 and
// Cha Lebagta's 97 up to Prog 2; Ardea's 261 at Prog 0 (option 0: Prog 1, the stone taken, 10 gil; option 1: Option 1),
// 262 at Prog 1, 263 after; Varun's 100 at Prog 2 while Option < 2 (Option 2), then 101. The Palborough Mines Mythril
// Seam's script gives the Sharp Stone from Prog 2 and sets Prog 3. Varun's 102 (2100 gil) completes it (fame 30; 2100
// gil; the quest's mustZone). Completed: Bopa Greso's 96, Cha Lebagta's 97 and Varun's 103 while the mustZone holds,
// else Varun's 104 (replaceDefault).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): Varun's trade wants the Sharp Stone
// (npcUtil.tradeHas(trade, xi.item.SHARP_STONE)), and 102's confirmTrade takes it. The Lua's test
// npcUtil.tradeHas(trade, { { xi.item.SHARP_STONE } }) names a one-element table, which tradeHas does not read as an
// item (it prints its ERROR line and needs nothing): any trade played 102, and confirmTrade took nothing.
// Kept as the Lua file has it: the trade reads no progress (any Prog of the accepted quest); Nanaa Mihgo answers nil at
// Prog 3, Varun at Prog 0, 1 and 3.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class RockRacketeer : QuestScript
    {
        public RockRacketeer() : base(QuestLog.Windurst, "ROCK_RACKETEER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Rock_Racketeer";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "MIHGOS_AMIGO") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "MIHGOS_AMIGO"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.ProgressEvent(93))
                .OnEventFinish(Zone.WindurstWoods, 93, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.SharpGrayStone);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(q =>
                {
                    // Once the player gets the Sharp Stone, NPC goes back to default action until after the quest is complete
                    if (q.Quest.Step <= 2)
                    {
                        return QuestReply.Event(96);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(q =>
                {
                    // Once the player gets the Sharp Stone, NPC goes back to default action until after the quest is complete
                    if (q.Quest.Step <= 2)
                    {
                        return QuestReply.Event(97);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        if (q.Quest.Var("Option") == 0)
                        {
                            return QuestReply.Event(94);
                        }
                        else
                        {
                            return QuestReply.Event(95);
                        }
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(98);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(99);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Varun)
                    // It was found in captures that the dialogue for Varun is not required to progress the quest
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 2)
                        {
                            if (q.Quest.Var("Option") < 2)
                            {
                                return QuestReply.ProgressEvent(100); // Optional Dialogue that only places once
                            }
                            else
                            {
                                return QuestReply.Event(101, 0, Item.SharpStone);
                            }
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, xi.item.SHARP_STONE): the Lua's { { xi.item.SHARP_STONE } } read no
                        // item and held for any trade (fixed, user ruling 2026-11-04)
                        if (q.Trade.Has(Item.SharpStone))
                        {
                            return QuestReply.ProgressEvent(102, 2100);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWoods, 98, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.WindurstWoods, 100, q => q.Quest.SetVar("Option", 2))
                .OnEventFinish(Zone.WindurstWoods, 102, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddGil(2100); // "Obtained X gil." Text is baked into the CS, so gil needs to be given outside of the quest rewards.
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Ardea).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(261);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(262);
                    }
                    else
                    {
                        return QuestReply.Event(263);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 261, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                        q.Player.DelKeyItem(KeyItem.SharpGrayStone); // Silent
                        q.Player.GiveCurrency("gil", 10);
                    }
                    else if (q.Option == 1)
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(96);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(97);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Varun).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(103);
                    }
                    else
                    {
                        return QuestReply.Event(104).ReplaceDefault();
                    }
                });
        }
    }
}
