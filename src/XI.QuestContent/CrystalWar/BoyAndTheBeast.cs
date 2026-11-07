// Boy and the Beast (Log ID 7, Quest ID 24), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_3_Boy_and_the_Beast.lua (quest rework B3b Crystal War group 6). Southern San
// d'Oria [S]: Raustigne !pos 4 -2 44.5 (default 606), Rholont !pos -168 -2 56 (default 32). Vunkerl Inlet [S]: qm7
// !pos -26 -31 364 (no default), Leafy Patch !pos -418 -33 576 (default 106). Available, the WotG mission Back to the
// Beginning and Claws of the Griffon completed (the Prog var is set while the quest is still available): Raustigne:
// Prog 0: 55 (-> 1). Rholont: Prog 1: 56 (-> 2); Prog 2: 57 (an Event). qm7: Prog 2: 105, whose finish gives the
// Vunkerl Herb Memo and begins it. Accepted: qm7: the Vunkerl Herb held: Prog 3: 108, whose finish completes it (a
// Carbon Fishing Rod) and takes the herb and the memo; else 109, whose finish takes the herb; no herb: Option 0: 110
// (an Event, whose finish sets Option 1); else 111 (an Event). Leafy Patch: no herb: 107 with the hour param (0
// before 08:00, 1 from 08:00, 2 from 16:00), whose finish sets Prog 3 on option 1 and gives the herb (on any option).
// Kept as the Lua has it: the Lua's note says Option is "reset every time a different event from this point fires",
// but nothing resets it (111 from the second talk on); 107 gives the herb on any option (only option 1 sets Prog 3),
// so 109 takes it back on a wrong answer; Leafy Patch answers nil while the herb is held; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BoyAndTheBeast : QuestScript
    {
        public BoyAndTheBeast() : base(QuestLog.CrystalWar, "BOY_AND_THE_BEAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_3_Boy_and_the_Beast";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                Item = Item.CarbonFishingRod,
            });

            b.State(QuestStatus.Available, when: q =>
                    // WotG mission requirement, then the WotG nation quest requirement.
                    q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.BackToTheBeginning) &&
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "CLAWS_OF_THE_GRIFFON"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(55);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(56);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(57);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 55, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 56, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm7).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(105);
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 105, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.VunkerlHerbMemo);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm7).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.VunkerlHerb))
                    {
                        if (q.Quest.Step == 3)
                        {
                            return QuestReply.ProgressEvent(108);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(109);
                        }
                    }
                    else
                    {
                        // NOTE: The below two events are "Nothing out of the Ordinary" events; however, Event 110
                        // is only displayed the first time in this state.  Unknown use at this time, but implemented
                        // for accuracy.  This Option var is actually reset every time a different event from this
                        // point fires.

                        if (q.Quest.Var("Option") == 0)
                        {
                            return QuestReply.Event(110);
                        }
                        else
                        {
                            return QuestReply.Event(111);
                        }
                    }
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.LeafyPatch).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.VunkerlHerb))
                    {
                        int vanadielHour = q.Player.VanadielHour;
                        int hourParam = 0;

                        if (vanadielHour >= 16)
                        {
                            hourParam = 2;
                        }
                        else if (vanadielHour >= 8)
                        {
                            hourParam = 1;
                        }

                        return QuestReply.ProgressEvent(107, hourParam);
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 107, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(3);
                    }

                    q.Player.GiveKeyItem(KeyItem.VunkerlHerb);
                })
                .OnEventFinish(Zone.VunkerlInletS, 108, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.VunkerlHerb);
                        q.Player.DelKeyItem(KeyItem.VunkerlHerbMemo);
                    }
                })
                .OnEventFinish(Zone.VunkerlInletS, 109, q =>
                {
                    q.Player.DelKeyItem(KeyItem.VunkerlHerb);
                })
                .OnEventFinish(Zone.VunkerlInletS, 110, q =>
                {
                    q.Quest.SetVar("Option", 1);
                });
        }
    }
}
