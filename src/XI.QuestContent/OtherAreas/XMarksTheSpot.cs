// X Marks the Spot (Log ID 4, Quest ID 65), a line-for-line port of scripts/quests/otherAreas/X_Marks_The_Spot.lua (quest
// rework B3b). Tavnazian Safehold: Despachiaire !pos 111.2090 -40.0148 -85.4810 (default 106), Parelbriaux !pos 113.701 -41
// 42.3653 (two of the name, 16883726 and 16883762, the second cutscene_only; default 294), Odeya !pos 83.3227 -34.25 -70.0384 (default 150), no scripts; Phomiuna Aqueducts:
// the Hidden Hallway's 2nd gate _0r9 !pos 138.1027 -24 60.3209 (a script with no handlers; default DOOR_FIRMLY_SHUT).
// Available once the char's current CoP mission is Ancient Vows or later: Despachiaire 144, whose finish begins the quest.
// Accepted: Despachiaire 144 (importantOnce: Event the first time, then Default priority until the zone changes);
// Parelbriaux at Prog <= 1: 145 (Prog 1); Odeya at Prog 1: 141 (0, the Tavnazian Liver; Prog 2), 2: 146 (0, the liver), 3:
// 147, 4: 143 (completed: 4000 gil, then Post 1); a trade of exactly the liver at Prog 2: 142 (the liver taken, Prog 3).
// _0r9 at Prog 3: 37 (Prog 4); else nothing. Completed: Odeya with Post >= 1: 148 (Post 2); leaving Tavnazian Safehold at
// Post 2: quest:cleanup (the vars cleared: the post-quest line is never seen again).
// Kept as the Lua has it: 145 sets Prog 1 again at Prog 1 (and at Prog 0 the first time); 141 sets Prog 2 at any Prog;
// 144's finish begins the quest only from the available section (the accepted section has no 144 finish).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class XMarksTheSpot : QuestScript
    {
        public XMarksTheSpot() : base(QuestLog.OtherAreas, "X_MARKS_THE_SPOT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/X_Marks_The_Spot";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 4000,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q => q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.AncientVows)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.ProgressEvent(144))
                .OnEventFinish(Zone.TavnazianSafehold, 144, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                // Will repeat until the quest is completed.
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.Event(144).ImportantOnce())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Parelbriaux).OnTrigger(q =>
                {
                    if (q.Quest.Step <= 1)
                    {
                        return QuestReply.ProgressEvent(145);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Odeya).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(141, 0, Item.TavnazianLiver);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(146, 0, Item.TavnazianLiver);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.Event(147);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(143);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Quest.Step == 2 &&
                        q.Trade.HasExactly(Item.TavnazianLiver))
                    {
                        return QuestReply.ProgressEvent(142);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 141, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 142, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 143, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("Post", 1);
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 145, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.PhomiunaAqueducts, Npc.PhomiunaAqueducts._0r9).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(37);
                    }
                    else
                    {
                        return null;
                    }
                })
                .OnEventFinish(Zone.PhomiunaAqueducts, 37, q =>
                {
                    q.Quest.SetStep(4);
                });

            // Post Quest Dialogue, once the player gets this dialogue it will continue until the player zones.
            // After zoning this is never seen again.
            var completed = b.State(QuestStatus.Completed);
            completed
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Odeya).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") >= 1)
                    {
                        return QuestReply.Event(148);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 148, q =>
                {
                    q.Quest.SetVar("Post", 2);
                });

            // OnZoneOut(zone, Action<IQuestContext>) (P7) = the section's [xi.zone.TAVNAZIAN_SAFEHOLD].onZoneOut = function(player)
            completed.OnZoneOut(Zone.TavnazianSafehold, q =>
            {
                if (q.Quest.Var("Post") == 2)
                {
                    // quest:cleanup(player): the quest's vars cleared, as quest:complete does after the completion
                    q.Quest.Cleanup();
                }
            });
        }
    }
}
