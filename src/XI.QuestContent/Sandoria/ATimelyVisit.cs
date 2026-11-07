// A Timely Visit (Log ID 0, Quest ID 105), a line-for-line port of scripts/quests/sandoria/A_Timely_Visit.lua (quest
// rework B3b, San d'Oria group 7). Deraquien : !pos -98.698 -2.0 32.056; Narvecaint : !pos -261.936 23.144 127.61;
// Phillone : !pos -208.832 -2.949 67.189; qm1 : !pos -310.882 -0.139 407.377. Offered at San d'Oria fame level 4:
// Deraquien's 33 (option 1: begun; else the var Option 1), then 47 (option 1: begun, Option 0). Accepted, Stage 0: Prog
// walks 0-5: Narvecaint's 0 (Prog 1), Deraquien's 80 (Prog 2), Halver's 112 (Prog 3), Deraquien's 87 (Prog 4),
// Phillone's 78 (Prog 5), Deraquien's 38 (Stage 1); between them the reminders 34 / 20 / 30 (Deraquien), 6 / 7
// (Narvecaint), 91 (Phillone), each at priority 101 (over the NPC scripts). Stage 1: in Jugner Forest, qm1 at Prog 5: at
// night with the wait over and neither NM up it pops Giollemitte B'Feroun and the Skeleton Esquire (claimed;
// SENSE_OF_FOREBODING); by day UNABLE_TO_INVESTIGATE; after either NM's death (the char local var NMKilled) and with both
// gone, 18 (Prog 6). Phillone's 21 (Prog 7), Narvecaint's 1 (Prog 8), Phillone's 14 (completed: Medieval Collar, fame
// +40, Obsidian Storm); reminders Deraquien's 27, Narvecaint's 7 / 8, Phillone's 48 / 49. Completed: Phillone's 28
// (replaceDefault). The NMs' mob scripts set qm1's local var Wait on despawn (GetSystemTime() + 180), which the qm reads
// (stays). goblinfootprint.lua reads the status (stays).
// Kept as the Lua has it: 14's finish ignores quest:complete's result; Halver at another Prog, Narvecaint past Prog 5 in
// Stage 0, and qm1 off Prog 5 return nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ATimelyVisit : QuestScript
    {
        public ATimelyVisit() : base(QuestLog.Sandoria, "A_TIMELY_VISIT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Timely_Visit";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Sandoria,
                Item = Item.MedievalCollar,
                Title = Title.ObsidianStorm,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 4)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Deraquien).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(33);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(47);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 33, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        // Player declined to start quest, and flags the secondary dialogue to start quest
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 47, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        // Resets variable
                        q.Quest.SetVar("Option", 0);
                    }
                });

            // Pre NM Fight
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("Stage") == 0)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(112);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 112, q => q.Quest.SetStep(3))
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Narvecaint).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(0);
                    }
                    else if (progress <= 2)
                    {
                        return QuestReply.Event(6).SetPriority(101);
                    }
                    else if (progress <= 5)
                    {
                        return QuestReply.Event(7).SetPriority(101);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LaTheinePlateau, 0, q => q.Quest.SetStep(1))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Deraquien).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(34).SetPriority(101);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(80);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(20).SetPriority(101);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.ProgressEvent(87);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.Event(30).SetPriority(101);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.ProgressEvent(38);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Phillone).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(78);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(91).SetPriority(101);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 38, q => q.Quest.SetVar("Stage", 1))
                .OnEventFinish(Zone.SouthernSanDoria, 78, q => q.Quest.SetStep(5))
                .OnEventFinish(Zone.SouthernSanDoria, 80, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.SouthernSanDoria, 87, q => q.Quest.SetStep(4));

            // NM fight to end
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("Stage") == 1)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.qm1).OnTrigger(q =>
                {
                    int hour = q.Player.VanadielHour;
                    int progress = q.Quest.Step;
                    bool mobsSpawned = q.Player.MobIsSpawned(Mob.JugnerForest.GIOLLEMITTE, 0) ||
                        q.Player.MobIsSpawned(Mob.JugnerForest.GIOLLEMITTE, 1);
                    bool waitOver = q.Player.SystemTime >= q.Npc.LocalVar("Wait");
                    bool isDaytime = hour >= 6 && hour <= 17;

                    if (q.Player.LocalVar("NMKilled") == 1 && progress == 5 && !mobsSpawned)
                    {
                        return QuestReply.ProgressEvent(18);
                    }
                    else if (progress == 5 && waitOver && !mobsSpawned && !isDaytime)
                    {
                        for (int i = 0; i <= 1; i++)
                        {
                            q.Player.SpawnMob(Mob.JugnerForest.GIOLLEMITTE, i, claim: true);
                        }

                        return QuestReply.MessageSpecial(Text.JugnerForest.SENSE_OF_FOREBODING);
                    }
                    else if (progress == 5 && isDaytime)
                    {
                        return QuestReply.MessageSpecial(Text.JugnerForest.UNABLE_TO_INVESTIGATE);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForest, 18, q => q.Quest.SetStep(6))
                // Player can kill EITHER NM to progress
                .Npc(Zone.JugnerForest, Npc.JugnerForest.GiollemitteBFeroun).OnMobDeath(q =>
                {
                    // mob:getID() == jugnerID.mob.GIOLLEMITTE
                    if (q.Mob.Id == q.Player.MobIdOf(Mob.JugnerForest.GIOLLEMITTE) &&
                        q.Quest.Step == 5)
                    {
                        q.Player.SetLocalVar("NMKilled", 1);
                    }
                })
                // Player can kill EITHER NM to progress
                .Npc(Zone.JugnerForest, Npc.JugnerForest.SkeletonEsquireNM).OnMobDeath(q =>
                {
                    // mob:getID() == jugnerID.mob.GIOLLEMITTE + 1
                    if (q.Mob.Id == q.Player.MobIdOf(Mob.JugnerForest.GIOLLEMITTE) + 1 &&
                        q.Quest.Step == 5)
                    {
                        q.Player.SetLocalVar("NMKilled", 1);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.Narvecaint).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress <= 6)
                    {
                        return QuestReply.Event(7).SetPriority(101);
                    }
                    else if (progress == 7)
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    else
                    {
                        return QuestReply.Event(8).SetPriority(101);
                    }
                })
                .OnEventFinish(Zone.LaTheinePlateau, 1, q => q.Quest.SetStep(8))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Deraquien).OnTrigger(QuestReply.Event(27).SetPriority(101))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Phillone).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress <= 5)
                    {
                        return QuestReply.Event(48).SetPriority(101);
                    }
                    else if (progress == 6)
                    {
                        return QuestReply.ProgressEvent(21);
                    }
                    else if (progress == 8)
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    else
                    {
                        return QuestReply.Event(49).SetPriority(101);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 14, q => q.Quest.Complete())
                .OnEventFinish(Zone.SouthernSanDoria, 21, q => q.Quest.SetStep(7));

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Phillone).OnTrigger(QuestReply.Event(28).ReplaceDefault());
        }
    }
}
