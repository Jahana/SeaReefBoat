// Crying Over Onions (Log ID 2, Quest ID 76), a line-for-line port of scripts/quests/windurst/SOB5_Crying_Over_Onions.lua
// (quest rework B3b). Kohlo-Lakolo : !pos -26.8 -6 190 240; Honoi-Gomoi (Windurst Waters); Nanaa Mihgo (Windurst Woods).
// Once Onion Rings is completed, Kohlo-Lakolo's 449 first gives that quest's reward, the Bouncer Club (Reward 1, this
// quest's mustZone); after a zone change, at level 5 and Windurst fame level 5, his 496 begins it (450 otherwise; 440
// while the mustZone holds before the club). Accepted: Honoi-Gomoi's 774 (0, Star Spinel) at Prog 0 (Prog 1), 777 at
// Prog 1-2, Nanaa Mihgo's optional 598 at Prog 1 (Prog 2); the Star Spinel traded alone at Prog 1 or 2 plays 775 (the
// Star Necklace given: the spinel taken, Prog 3), Honoi-Gomoi's 778 at Prog 3; Kohlo-Lakolo's 497 at Prog 3 (Prog 4;
// 498 before, 512 after); the brigade's lines at Prog 4; Honoi-Gomoi's 776 at Prog 4 completes it (fame 50). Completed
// while The Promise is available: the brigade's new defaults (replaceDefault). Kept as the Lua file has it: 776 sets the
// player local var '[2][77]mustZone', which nothing reads; the reward has no item, so the completion cannot fail.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CryingOverOnions : QuestScript
    {
        public CryingOverOnions() : base(QuestLog.Windurst, "CRYING_OVER_ONIONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB5_Crying_Over_Onions";

        // the brigade's Prog 4 lines (the same shape for each NPC in Lua)
        static QuestReply AtProg4(IQuestContext q, int csid)
        {
            if (q.Quest.Step == 4)
            {
                return QuestReply.Event(csid);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 50, FameArea = FameArea.Windurst });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Windurst, "ONION_RINGS"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Quest.Var("Reward") == 1)
                    {
                        if (q.Player.MainLevel >= 5 &&
                            q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                            !q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(496); // Quest starting event.
                        }
                        else
                        {
                            return QuestReply.Event(450);
                        }
                    }
                    else
                    {
                        if (!q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(449); // Get previous quest reward.
                        }
                        else
                        {
                            return QuestReply.Event(440);
                        }
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 449, q =>
                {
                    if (q.Player.GiveItem(Item.BouncerClub))
                    {
                        q.Quest.SetVar("Reward", 1);
                        q.Quest.SetMustZone();
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 496, q => q.Quest.Begin());

            // Section: Quest accepeted in time.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(497);
                    }
                    else if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(512); // Reminder text.
                    }
                    else
                    {
                        return QuestReply.Event(498); // Reminder text.
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(q => AtProg4(q, 500))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(q => AtProg4(q, 501))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(q => AtProg4(q, 503))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(q => AtProg4(q, 502))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(q => AtProg4(q, 504))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(q => AtProg4(q, 499))
                .OnEventFinish(Zone.PortWindurst, 497, q => q.Quest.SetStep(4))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HonoiGomoi)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(774, 0, Item.StarSpinel);
                        }
                        else if (q.Quest.Step == 1 || q.Quest.Step == 2)
                        {
                            return QuestReply.Event(777); // Reminder text before trade.
                        }
                        else if (q.Quest.Step == 3)
                        {
                            return QuestReply.Event(778); // Reminder text after trade
                        }
                        else if (q.Quest.Step == 4)
                        {
                            return QuestReply.ProgressEvent(776); // Quest Complete.
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if ((q.Quest.Step == 1 || q.Quest.Step == 2) &&
                            q.Trade.HasExactly(Item.StarSpinel))
                        {
                            return QuestReply.ProgressEvent(775, 0, Item.StarSpinel);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 774, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.WindurstWaters, 775, q =>
                {
                    if (q.Player.GiveItem(Item.StarNecklace))
                    {
                        q.Trade.Confirm();
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 776, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("[2][77]mustZone", 1);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(598); // Optional, one time only, dialog.
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 598, q => q.Quest.SetStep(2));

            // Section: Quest completed.
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "THE_PROMISE") == QuestStatus.Available)
                // New default texts.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(507).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(508).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(511).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(510).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(504).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(506).ReplaceDefault());
        }
    }
}
