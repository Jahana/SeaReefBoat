// In a Stew (Log ID 2, Quest ID 45), a line-for-line port of scripts/quests/windurst/In_a_Stew.lua (quest rework B3b).
// Kuoh Rhel : !pos 131.437 -6 -102.723; Matata : !pos 131 -5 -109; Ranpi-Monpi : !pos -116 -3 52. Repeatable, its
// sections read the quest's vars, not its status. Prog 0, Wait before the next conquest tally, Chocobilious completed,
// Windurst fame level 2, Chocobilious's mustZone clear: Kuoh Rhel's 235 while available (begun, Prog 1), else 234
// (option 1: Prog 1). Prog >= 1: Ranpi-Monpi (Windurst Waters), while A Crisis in the Making is not accepted: 557 at
// Prog 2, else 555 (0, Woozyshroom) when Option is 1 or Chocobilious is completed, else 554 (Option 1); three
// Woozyshrooms traded exactly: 556 (taken; Ranpi-Monpi's Special Stew; Prog 2); Kuoh Rhel's 239 at Prog 2 (completed:
// fame 10, 900 gil; the quest's mustZone; the stew taken; Wait = the next conquest tally), else 236; Matata's 237.
// Completed: Kuoh Rhel's 240 and Matata's 241 while the mustZone holds.
// Kept as the Lua file has it: Ranpi-Monpi's 556 trade tests no Prog (at Prog 2 a second batch plays 556 again) and
// not A Crisis in the Making; Chocobilious's own completion (Kuoh Rhel's script) sets the generic needToZone flag, not
// the Chocobilious mustZone this check reads (only a GM sets that).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class InAStew : QuestScript
    {
        public InAStew() : base(QuestLog.Windurst, "IN_A_STEW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/In_a_Stew";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Windurst, Gil = 900 });

            // Lua: vars.Prog == 0 and vars.Wait < NextConquestTally() and ... (no status test)
            b.AnyState(when: q =>
                    q.Quest.Step == 0 &&
                    q.Quest.Var("Wait") < q.Player.NextConquestTally &&
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "CHOCOBILIOUS") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "CHOCOBILIOUS"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KuohRhel).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.Windurst, "IN_A_STEW") == QuestStatus.Available)
                    {
                        return QuestReply.ProgressEvent(235);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(234); // Repeat after conquest tally
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 234, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 235, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Lua: vars.Prog >= 1
            b.AnyState(when: q =>
                    q.Quest.Step >= 1)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KuohRhel).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(239);
                    }
                    else
                    {
                        return QuestReply.Event(236); // Reminder
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Matata).OnTrigger(q =>
                {
                    return QuestReply.Event(237); // Gives the player a hint to talk to Ranpi-Monpi
                })
                .OnEventFinish(Zone.WindurstWoods, 239, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                        q.Player.DelKeyItem(KeyItem.RanpiMonpisSpecialStew);
                        q.Quest.SetVar("Wait", q.Player.NextConquestTally);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi)
                    .OnTrigger(q =>
                    {
                        int optional = q.Quest.Var("Option");

                        if (q.Player.QuestStatus(QuestLog.Windurst, "A_CRISIS_IN_THE_MAKING") != QuestStatus.Accepted) // The player CANNOT have the quest "A Crisis in the Making" Active or Ranpi will not talk to you about this quest
                        {
                            if (q.Quest.Step == 2)
                            {
                                return QuestReply.Event(557); // Reminder to finish quest
                            }
                            else if (
                                optional == 1 ||
                                q.Player.HasCompletedQuest(QuestLog.Windurst, "CHOCOBILIOUS"))
                            {
                                return QuestReply.Event(555, 0, Item.Woozyshroom); // Reminder to get Woozyshrooms
                            }
                            else if (optional == 0)
                            {
                                return QuestReply.Event(554, 0, Item.Woozyshroom); // Requests 3x Woozyshrooms. Only plays the first time the player does the quest
                            }
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.Woozyshroom, 3)))
                        {
                            return QuestReply.ProgressEvent(556);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 554, q => q.Quest.SetVar("Option", 1))
                .OnEventFinish(Zone.WindurstWaters, 556, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.RanpiMonpisSpecialStew);
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KuohRhel).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(240);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Matata).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(241);
                    }

                    return null;
                });
        }
    }
}
