// Inspector's Gadget! (Log ID 2, Quest ID 41), a line-for-line port of scripts/quests/windurst/SOB3_Inspectors_Gadget.lua
// (quest rework B3b). Kohlo-Lakolo : !pos -26.8 -6 190 240; Chamama (Windurst Waters). Once Know One's Onions is
// completed, Kohlo-Lakolo's 413 offers it at level 5 and Windurst fame level 2 (option 0: begun), else his 401.
// Accepted: Kohlo-Lakolo 414 (421 with the Fake Moustache: completed, fame 20, the Heko Obi, Fake-Moustached
// Investigator); the brigade's reminder lines, Pichichi's 415 setting Prog 1. Chamama: 551 at Prog 1 or more (Prog 1:
// Prog 2), 553 with the moustache; four balls of Saruta cotton traded alone at Prog 2 without the moustache play 552
// (taken, the Fake Moustache). Completed while Onion Rings is available: the brigade's new defaults (replaceDefault).
// Kept as the Lua file has it: the offer reads this quest's char-var mustZone, which nothing sets; 421 sets the player
// local var '[2][42]mustZone', which nothing reads; the moustache stays after the completion; Chamama's 551 plays again
// at Prog 2 (with no moustache) and does nothing more.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class InspectorsGadget : QuestScript
    {
        public InspectorsGadget() : base(QuestLog.Windurst, "INSPECTORS_GADGET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB3_Inspectors_Gadget";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.HekoObi,
                Title = Title.FakeMoustachedInvestigator,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Windurst, "KNOW_ONES_ONIONS"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 5 &&
                        q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                        !q.Quest.MustZone)
                    {
                        return QuestReply.ProgressEvent(413); // Quest starting event.
                    }
                    else
                    {
                        return QuestReply.Event(401); // Default text.
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 413, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FakeMoustache))
                    {
                        return QuestReply.ProgressEvent(421);
                    }
                    else
                    {
                        return QuestReply.Event(414);
                    }
                })
                // Reminder text.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(417))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(416))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(415, 0, KeyItem.FakeMoustache))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(418))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(420))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(419, 0, Item.Linkshell))
                .OnEventFinish(Zone.PortWindurst, 415, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 421, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("[2][42]mustZone", 1);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.FakeMoustache))
                        {
                            return QuestReply.Event(553);
                        }
                        else
                        {
                            if (q.Quest.Step > 0)
                            {
                                return QuestReply.ProgressEvent(551, 0, KeyItem.FakeMoustache);
                            }
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 2 &&
                            q.Trade.HasExactly((Item.BallOfSarutaCotton, 4)) &&
                            !q.Player.HasKeyItem(KeyItem.FakeMoustache))
                        {
                            return QuestReply.ProgressEvent(552);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 551, q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 552, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.FakeMoustache);
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "ONION_RINGS") == QuestStatus.Available)
                // New default texts.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(425).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(424).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(423).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(426).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(428).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(427).ReplaceDefault());
        }
    }
}
