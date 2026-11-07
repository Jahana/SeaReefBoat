// In Search of the Truth (Log ID 4, Quest ID 80), a line-for-line port of scripts/quests/otherAreas/In_Search_of_the_Truth.lua
// (quest rework B3b). Tavnazian Safehold, no scripts: Tressia !pos 87.527 -33.991 70.481 (two, 16883723 and +36;
// default 310), Raminey (159), Zadant (326), Fouagine (338), Noam (363), Mengrenaux (two; 270), Chemioue (two; 280),
// Ondieulix (two; 285), qm1..qm5_in_search_of_truth (default NOTHING_OUT_OF_ORDINARY).
// Available once the char's current CoP mission is past Darkness Named: Tressia 544 (option 1 = the Shaded Cruse), whose
// finish begins the quest. Accepted, Prog 0: Raminey / Zadant / Fouagine / Noam, each while its bit (1 / 2 / 3 / 4) of
// Option is unset: 549 / 551 / 553 / 555 (the bit set); Tressia at Option 30 (all four bits): 557, else 556 (event); 557
// option 1: Option 0, Prog 1. Prog 1: Tressia 565 (Prog 2); Mengrenaux 560, Chemioue 561 (events). Prog 2: Tressia at
// Option 1: 562, Option 0: 566 (event); qm1 with no cruse and Option 0: the cruse (player:addKeyItem), Water 1,
// CRUSE_ON_THE_GROUND; qm1 with the cruse: TRAIL_OF_WATER (message); qm2 / qm3 / qm4 with the cruse at Water >= 1 / 2 / 3:
// Water 2 / 3 / 4, TRAIL_OF_WATER; qm5 with the cruse at Water 4: 558 (option 1 = the cruse), whose finish takes the cruse,
// sets Option 1, Water 0 and completes the quest (player:completeQuest: no reward). Completed: Tressia at Option >= 1: 562
// (Option 2), else 314 (event, replaceDefault); Ondieulix at Option 2: 559, whose finish sets Option 0 and runs
// quest:complete (the Gramary Cape).
// Kept as the Lua has it: 559 clears Option before quest:complete, so a full inventory loses the reward for good (Tressia
// then answers 314); the Prog 2 Tressia's Option 1 arm is reached only by a GM (558 completes the quest at once), and its
// 562 has no finish while accepted; qm2..qm4 test Water >= n and set n + 1, so an earlier ??? talked to again sets Water
// back down (qm2 at Water 3: Water 2); qm1 resets nothing; 557 at other options does nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class InSearchOfTheTruth : QuestScript
    {
        public InSearchOfTheTruth() : base(QuestLog.OtherAreas, "IN_SEARCH_OF_THE_TRUTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/In_Search_of_the_Truth";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.GramaryCape,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.DarknessNamed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Tressia).OnTrigger(QuestReply.ProgressEvent(544).Option(1, KeyItem.ShadedCruse))
                .OnEventFinish(Zone.TavnazianSafehold, 544, q =>
                {
                    q.Quest.Begin();
                });

            // First portion of quest
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Tressia).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 30)
                    {
                        return QuestReply.ProgressEvent(557);
                    }
                    else
                    {
                        return QuestReply.Event(556);
                    }
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Raminey).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 1))
                    {
                        return QuestReply.ProgressEvent(549);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Zadant).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 2))
                    {
                        return QuestReply.ProgressEvent(551);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Fouagine).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 3))
                    {
                        return QuestReply.ProgressEvent(553);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Noam).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 4))
                    {
                        return QuestReply.ProgressEvent(555);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 549, q =>
                {
                    q.Quest.SetVarBit("Option", 1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 551, q =>
                {
                    q.Quest.SetVarBit("Option", 2);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 553, q =>
                {
                    q.Quest.SetVarBit("Option", 3);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 555, q =>
                {
                    q.Quest.SetVarBit("Option", 4);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 557, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("Option", 0);
                        q.Quest.SetStep(1);
                    }
                });

            // Second portion of quest
            // Does not care if you talked to Mengrenaux or Chemioue. Will progress if you enter the correct order after talking to Tressia.
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Tressia).OnTrigger(QuestReply.ProgressEvent(565))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Mengrenaux).OnTrigger(QuestReply.Event(560))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Chemioue).OnTrigger(QuestReply.Event(561))
                .OnEventFinish(Zone.TavnazianSafehold, 565, q =>
                {
                    q.Quest.SetStep(2);
                });

            // Third portion of quest
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Tressia).OnTrigger(q =>
                {
                    int options = q.Quest.Var("Option");
                    if (options == 1)
                    {
                        return QuestReply.ProgressEvent(562);
                    }
                    else if (options == 0)
                    {
                        return QuestReply.Event(566);
                    }
                    return null;
                })
                // ???'s for the trail must be checked in order.
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qm1insearchoftruth).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.ShadedCruse) &&
                        q.Quest.Var("Option") == 0)
                    {
                        q.Player.AddKeyItem(KeyItem.ShadedCruse);
                        q.Quest.SetVar("Water", 1);
                        return QuestReply.MessageSpecial(Text.TavnazianSafehold.CRUSE_ON_THE_GROUND, KeyItem.ShadedCruse);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ShadedCruse))
                    {
                        return QuestReply.Message(q.Text.Id(Text.TavnazianSafehold.TRAIL_OF_WATER));
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qm2insearchoftruth).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShadedCruse) &&
                        q.Quest.Var("Water") >= 1)
                    {
                        q.Quest.SetVar("Water", 2);
                        return QuestReply.Message(q.Text.Id(Text.TavnazianSafehold.TRAIL_OF_WATER));
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qm3insearchoftruth).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShadedCruse) &&
                        q.Quest.Var("Water") >= 2)
                    {
                        q.Quest.SetVar("Water", 3);
                        return QuestReply.Message(q.Text.Id(Text.TavnazianSafehold.TRAIL_OF_WATER));
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qm4insearchoftruth).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShadedCruse) &&
                        q.Quest.Var("Water") >= 3)
                    {
                        q.Quest.SetVar("Water", 4);
                        return QuestReply.Message(q.Text.Id(Text.TavnazianSafehold.TRAIL_OF_WATER));
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qm5insearchoftruth).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShadedCruse) &&
                        q.Quest.Var("Water") == 4)
                    {
                        return QuestReply.ProgressEvent(558).Option(1, KeyItem.ShadedCruse);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 558, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ShadedCruse);
                    q.Quest.SetVar("Option", 1);
                    q.Quest.SetVar("Water", 0);
                    // player:completeQuest(xi.questLog.OTHER_AREAS, xi.quest.id.otherAreas.IN_SEARCH_OF_THE_TRUTH): the completed bit, no reward
                    q.Player.CompleteQuest(QuestLog.OtherAreas, "IN_SEARCH_OF_THE_TRUTH");
                });

            // Post Final Cutscene Stuff including getting the reward
            b.State(QuestStatus.Completed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Tressia).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") >= 1)
                    {
                        return QuestReply.ProgressEvent(562);
                    }
                    else
                    {
                        return QuestReply.Event(314).ReplaceDefault();
                    }
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Ondieulix).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 2)
                    {
                        return QuestReply.ProgressEvent(559);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 559, q =>
                {
                    q.Quest.SetVar("Option", 0);
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.TavnazianSafehold, 562, q =>
                {
                    q.Quest.SetVar("Option", 2);
                });
        }
    }
}
