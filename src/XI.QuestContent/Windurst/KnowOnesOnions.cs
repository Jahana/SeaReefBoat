// Know One's Onions (Log ID 2, Quest ID 40), a line-for-line port of scripts/quests/windurst/SOB2_Know_Ones_Onions.lua
// (quest rework B3b). Kohlo-Lakolo : !pos -26.8 -6 190 240. Offered at level 5 once Truth, Justice, and the Onion Way! is
// completed. Below Windurst rank 3 ("in time") Kohlo-Lakolo's 391 begins it at Prog 1: 392; four Wild Onions traded
// alone play 398 (taken, Prog 2): 399, the House of the Hero's 288 (Prog 3); at Prog 3 his 400 completes it (fame 20,
// the Scroll of Blaze Spikes, SOB Super Hero). At rank 3 or more ("late") 388 begins it at Prog 0: his 389 (Prog 0), 387
// (Prog 1), 386 (Prog 2: its finish completes the quest); four onions play 390 (completed, taken). The brigade's lines
// change with each section; completed while Inspector's Gadget is available: their new defaults (replaceDefault). Kept
// as the Lua file has it: the offer's Kohlo-Lakolo line tests the level again (always true there) and reads this
// quest's char-var mustZone, which nothing sets (379 only by GM); the completions set the player local var
// '[2][41]mustZone', which nothing reads; Prog 0 below rank 3 (begun late, then the rank lowered by GM) and Prog 4 or
// more match no section; the in-time sections have no Shanruru line.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class KnowOnesOnions : QuestScript
    {
        public KnowOnesOnions() : base(QuestLog.Windurst, "KNOW_ONES_ONIONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB2_Know_Ones_Onions";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.ScrollOfBlazeSpikes,
                Title = Title.SobSuperHero,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 5 &&
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "TRUTH_JUSTICE_AND_THE_ONION_WAY"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 5 &&
                        !q.Quest.MustZone)
                    {
                        if (q.Player.Rank(Nation.Windurst) < 3)
                        {
                            return QuestReply.ProgressEvent(391, 0, Item.WildOnion); // Quest starting event. (In time)
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(388, 0, Item.WildOnion); // Quest starting event. (Missed action)
                        }
                    }
                    else
                    {
                        return QuestReply.Event(379); // Default text.
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 388, q => q.Quest.Begin())
                .OnEventFinish(Zone.PortWindurst, 391, q =>
                {
                    q.Quest.SetStep(1);
                    q.Quest.Begin();
                });

            // Section: Quest accepeted in time.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1 && q.Player.Rank(Nation.Windurst) < 3)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo)
                    .OnTrigger(q => QuestReply.Event(392, 0, Item.WildOnion)) // Reminder text if you are still in time.
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.WildOnion, 4)))
                        {
                            return QuestReply.ProgressEvent(398, 0, Item.WildOnion); // Trade in time. Quest goes on.
                        }

                        return null;
                    })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(394))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(393))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(395))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(396))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(397))
                .OnEventFinish(Zone.PortWindurst, 398, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(2);
                });

            // Section: Trade performed in time.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2 && q.Player.Rank(Nation.Windurst) < 3)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(394))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(QuestReply.Event(399, 0, Item.WildOnion))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(393))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(395))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(396))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(397))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(QuestReply.ProgressEvent(288, 0, Item.WildOnion))
                .OnEventFinish(Zone.WindurstWalls, 288, q => q.Quest.SetStep(3));

            // Section: Expedition complete. End quest.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(404))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(QuestReply.ProgressEvent(400))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(402))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(410))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(408))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(406))
                .OnEventFinish(Zone.PortWindurst, 400, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("[2][41]mustZone", 1);
                    }
                });

            // Section: Quest accepted. You are late at any step of the process, except very last step.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step < 3 && q.Player.Rank(Nation.Windurst) >= 3)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.Event(389, 0, Item.WildOnion); // Reminder text: Started quest late.
                        }
                        else if (q.Quest.Step == 1)
                        {
                            return QuestReply.Event(387, 0, Item.WildOnion); // Reminder text: In time for introduction but late before trade is done.
                        }
                        else if (q.Quest.Step == 2)
                        {
                            return QuestReply.Event(386); // Reminder text: Late after trade. Quest Complete.
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.WildOnion, 4)))
                        {
                            return QuestReply.ProgressEvent(390); // Quest Complete.
                        }

                        return null;
                    })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(405))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(403))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(411))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(409))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(412))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(407))
                .OnEventFinish(Zone.PortWindurst, 386, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("[2][41]mustZone", 1);
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 390, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetLocalVar("[2][41]mustZone", 1);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "INSPECTORS_GADGET") == QuestStatus.Available)
                // New default texts.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(405).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(403).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(411).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(409).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(412).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(407).ReplaceDefault());
        }
    }
}
