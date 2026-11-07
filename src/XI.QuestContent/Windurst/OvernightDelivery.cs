// Overnight Delivery (Log ID 2, Quest ID 15), a line-for-line port of scripts/quests/windurst/Overnight_Delivery.lua
// (quest rework B3b). Kenapa-Keppa : !pos 27 -6 -199 238; Kotan-Purutan : !pos 40.32 -9 44.24 249. Offered after Food for
// Thought at fame level 2 between 07:00 and 24:00: four talks (336-339) and the acceptance. Kotan-Purutan in Mhaura
// gives the Small Bag at night (141: before 06:00 or from 18:00) and records the due day (the same day when the finish
// runs before 06:00, else the next); Kenapa takes it before 06:00 of the due day (348: completed, Power Gi) or fails the
// quest (346: deleted, Prog 256, then 347 resets it). Water Way to Go reads its completion and mustZone. Kept as the
// Lua file has it: Kenapa's reminders (340-343) cycle Prog 4-7; a failed and reset offer starts the four talks again;
// the failure keeps no other trace (dueDate 0).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class OvernightDelivery : QuestScript
    {
        public OvernightDelivery() : base(QuestLog.Windurst, "OVERNIGHT_DELIVERY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Overnight_Delivery";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.PowerGi });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "FOOD_FOR_THOUGHT") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "FOOD_FOR_THOUGHT"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    int vanadielHour = q.Player.VanadielHour;

                    // Player has previously failed this quest
                    if (questProgress == 256)
                    {
                        return QuestReply.ProgressEvent(347);
                    }
                    // Quest Available Pattern (First attempt and previously failed)
                    else if (vanadielHour >= 7 && vanadielHour < 24)
                    {
                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(336);
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.ProgressEvent(337);
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.ProgressEvent(338);
                        }
                        else if (questProgress == 3)
                        {
                            return QuestReply.ProgressEvent(339);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 336, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.WindurstWaters, 337, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.WindurstWaters, 338, q => q.Quest.SetStep(3))
                .OnEventFinish(Zone.WindurstWaters, 339, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(4);
                    }
                    else
                    {
                        q.Quest.SetStep(0);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 347, q => q.Quest.SetStep(0));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.KotanPurutan).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (!q.Player.HasKeyItem(KeyItem.SmallBag) && questProgress >= 4 && questProgress <= 7)
                    {
                        int vanadielHour = q.Player.VanadielHour;

                        if (vanadielHour < 6 || vanadielHour >= 18)
                        {
                            return QuestReply.ProgressEvent(141);
                        }
                        else
                        {
                            return QuestReply.Event(144);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(142);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 141, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SmallBag);

                    // Track timing by day in which the KI is due to be delivered in order
                    // to simplify the logic for completing this quest.
                    if (q.Player.VanadielHour < 6)
                    {
                        q.Quest.SetVar("dueDate", q.Player.VanadielUniqueDay);
                    }
                    else
                    {
                        q.Quest.SetVar("dueDate", q.Player.VanadielUniqueDay + 1);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SmallBag))
                    {
                        int dueDate = q.Quest.Var("dueDate");
                        int currentDay = q.Player.VanadielUniqueDay;

                        if (dueDate > currentDay || (dueDate == currentDay && q.Player.VanadielHour < 6))
                        {
                            return QuestReply.ProgressEvent(348);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(346);
                        }
                    }
                    else
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 4)
                        {
                            return QuestReply.ProgressEvent(340);
                        }
                        else if (questProgress == 5)
                        {
                            return QuestReply.ProgressEvent(341);
                        }
                        else if (questProgress == 6)
                        {
                            return QuestReply.ProgressEvent(342);
                        }
                        else if (questProgress == 7)
                        {
                            return QuestReply.ProgressEvent(343);
                        }
                    }

                    return null;
                })
                // Kenapa-Keppa's reminder text cycles.  Events 340~343 loop
                // through the available messages.
                .OnEventFinish(Zone.WindurstWaters, 340, q => q.Quest.SetStep(5))
                .OnEventFinish(Zone.WindurstWaters, 341, q => q.Quest.SetStep(6))
                .OnEventFinish(Zone.WindurstWaters, 342, q => q.Quest.SetStep(7))
                .OnEventFinish(Zone.WindurstWaters, 343, q => q.Quest.SetStep(4))
                .OnEventFinish(Zone.WindurstWaters, 346, q =>
                {
                    q.Player.DelQuest(QuestLog.Windurst, "OVERNIGHT_DELIVERY");
                    q.Player.DelKeyItem(KeyItem.SmallBag);
                    q.Quest.SetVar("dueDate", 0);
                    q.Quest.SetStep(256);
                })
                .OnEventFinish(Zone.WindurstWaters, 348, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SmallBag);
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.KotanPurutan).OnTrigger(QuestReply.Event(143).ReplaceDefault())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Windurst) < 6)
                    {
                        return QuestReply.Event(351).ReplaceDefault();
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(q =>
                {
                    if (q.Player.RandomInt(1, 100) <= 50)
                    {
                        return QuestReply.Event(349).ReplaceDefault();
                    }
                    else
                    {
                        return QuestReply.Event(350).ReplaceDefault();
                    }
                });
        }
    }
}
