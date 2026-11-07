// Water Way to Go (Log ID 2, Quest ID 16), a line-for-line port of scripts/quests/windurst/Water_Way_to_Go.lua (quest
// rework B3b). Ohbiru-Dohbiru : !pos 23 -5 -193 238; Giddeus Spring : !pos -258 -2 -249 145. Offered after Overnight
// Delivery at fame level 3: Ohbiru gives a Rhinostery Canteen (on accepting), the Giddeus Spring fills it, and the full
// canteen completes the quest (fame +16, 900 gil, mustZone). Ohbiru gives another canteen when the char holds neither.
// Repeatable: once completed and zoned, Ohbiru offers it again below fame level 5 (Prog 1). Blue Ribbon Blues reads its
// completion. Kept as the Lua file has it: a repeat's quest:begin runs on a completed quest (the log's current bit is
// set, the status stays completed); while completed with Prog 0 the spring takes nothing, but Ohbiru still takes a full
// canteen and completes it again.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): the first hand-in ran both 355 finishes (the
// accepted state's completes the quest, then the completed state's matches too): 1800 gil and fame +32. The accepted
// state's finish sets the quest's local var firstComplete; the completed state's finish then only clears it (The
// Sweetest Things' latch), so the first hand-in pays 900 gil and fame +16 once.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class WaterWayToGo : QuestScript
    {
        public WaterWayToGo() : base(QuestLog.Windurst, "WATER_WAY_TO_GO") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Water_Way_to_Go";

        // not player:findItem(RHINOSTERY_CANTEEN) and not player:findItem(CANTEEN_OF_GIDDEUS_WATER)
        static bool HasNoCanteen(IQuestPlayerView player) =>
            !player.FindItem(Item.RhinosteryCanteen) && !player.FindItem(Item.CanteenOfGiddeusWater);

        // the spring's 55 (both sections have the same body)
        static void FinishSpring(IQuestContext q)
        {
            if (q.Player.GiveItem(Item.CanteenOfGiddeusWater))
            {
                q.Trade.Complete();
            }
        }

        // the hand-in's 355 (both sections have the same body); true when the quest completed
        static bool FinishHandIn(IQuestContext q)
        {
            if (q.Quest.Complete())
            {
                q.Trade.Complete();
                // Note: Message display for gil reward is handled by the event
                q.Player.AddGil(900);
                q.Quest.SetMustZone();
                return true;
            }
            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 16, FameArea = FameArea.Windurst });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "OVERNIGHT_DELIVERY") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "OVERNIGHT_DELIVERY"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrigger(QuestReply.ProgressEvent(352, 0, Item.CanteenOfGiddeusWater))
                .OnEventFinish(Zone.WindurstWaters, 352, q =>
                {
                    if (q.Option == 0 && q.Player.GiveItem(Item.RhinosteryCanteen))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Giddeus, Npc.Giddeus.GiddeusSpring).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.RhinosteryCanteen, 1)))
                    {
                        return QuestReply.ProgressEvent(55);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Giddeus, 55, FinishSpring)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.CanteenOfGiddeusWater, 1)))
                        {
                            return QuestReply.ProgressEvent(355, 900);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (HasNoCanteen(q.Player))
                        {
                            return QuestReply.ProgressEvent(354);
                        }
                        else
                        {
                            return QuestReply.Event(353);
                        }
                    })
                .OnEventFinish(Zone.WindurstWaters, 354, q => q.Player.GiveItem(Item.RhinosteryCanteen))
                .OnEventFinish(Zone.WindurstWaters, 355, q =>
                {
                    if (FinishHandIn(q))
                    {
                        // the latch: the completed state's 355 finish runs next in this lookup (user ruling 2026-11-05)
                        q.Quest.SetLocalVar("firstComplete", 1);
                    }
                });

            // Quest complete section.
            b.State(QuestStatus.Completed)
                .Npc(Zone.Giddeus, Npc.Giddeus.GiddeusSpring).OnTrade(q =>
                {
                    if (q.Quest.Step == 1 && q.Trade.Matches((Item.RhinosteryCanteen, 1)))
                    {
                        return QuestReply.ProgressEvent(55);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Giddeus, 55, FinishSpring)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.CanteenOfGiddeusWater, 1)))
                        {
                            return QuestReply.ProgressEvent(355, 900);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        // Default after completing without zoning.
                        if (q.Quest.MustZone)
                        {
                            return QuestReply.Event(356, 0, Item.CanteenOfGiddeusWater);
                        }

                        int questProgress = q.Quest.Step;

                        // Re-start quest.
                        if (questProgress == 0)
                        {
                            if (q.Player.FameLevel(FameArea.Windurst) < 5)
                            {
                                return QuestReply.ProgressEvent(352, 0, Item.CanteenOfGiddeusWater);
                            }
                        }
                        // Quest re-started.
                        else if (questProgress == 1)
                        {
                            if (HasNoCanteen(q.Player))
                            {
                                return QuestReply.ProgressEvent(354);
                            }
                            else
                            {
                                return QuestReply.Event(353);
                            }
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 352, q =>
                {
                    if (q.Option == 0 && q.Player.GiveItem(Item.RhinosteryCanteen))
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 354, q => q.Player.GiveItem(Item.RhinosteryCanteen))
                .OnEventFinish(Zone.WindurstWaters, 355, q =>
                {
                    // right after the accepted state's completion: paid already (user ruling 2026-11-05)
                    if (q.Quest.LocalVar("firstComplete") == 1)
                    {
                        q.Quest.SetLocalVar("firstComplete", 0);
                    }
                    else
                    {
                        FinishHandIn(q);
                    }
                });
        }
    }
}
