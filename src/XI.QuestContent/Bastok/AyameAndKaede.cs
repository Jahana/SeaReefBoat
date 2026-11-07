// Ayame and Kaede (Log ID 1, Quest ID 60), a line-for-line port of scripts/quests/bastok/Ayame_and_Kaede.lua (quest
// rework B3b). Kaede : !pos 48 -6 67 236; Kagetora : !pos -96 -2 29 236; Ensetsu : !pos 33 -6 67 236; qm2 : !pos -208
// -9 176 173; Ryoma : !pos -23 0 -9 252. From ADVANCED_JOB_LEVEL Kaede's 240 begins it. Kagetora's 241 (Prog 1),
// Ensetsu's 242 (Prog 2); at Korroloka Tunnel's qm2 the char spawns the three Korroloka Leeches (claimed,
// HAIR_STANDS_ON_END) and, after one of them dies while Prog is 2, gets the Strangely Shaped Coral (Prog 3; the qm2
// hides for FORCE_SPAWN_QM_RESET_TIME when this char spawned them); Ensetsu's 245 (Prog 4), Ryoma's 95 (the Sealed
// Dagger for the coral, Prog 5), Ensetsu's 246 completes it (Job Gesture: Ninja, Shadow Walker), the dagger goes and
// NIN is unlocked. Completed: Kaede's 248 (replaceDefault).
// Ensetsu's own script (scripts/zones/Port_Bastok/npcs/Ensetsu.lua) reads this quest's status to pick its 247, Twenty
// in Pirate Years' lead-in: a status read that starts none of this quest's events and writes none of its vars, so the
// script stays as it is (no xi.cs call).
// Kept as the Lua file has it: isNMDefeated is true as soon as one of the three leeches is not alive (isDead counts a
// mob never spawned or despawned as dead), so one kill is enough, and inside a leech's onMobDeath it is always true;
// the qm2 hides only for the char that spawned the leeches (isActor, the char's own local var).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AyameAndKaede : QuestScript
    {
        public AyameAndKaede() : base(QuestLog.Bastok, "AYAME_AND_KAEDE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Ayame_and_Kaede";

        // isNMSpawned: one of the three leeches (KORROLOKA_LEECH .. + 2) is up
        static bool IsNMSpawned(IQuestContext q)
        {
            for (int offset = 0; offset <= 2; offset++)
            {
                if (q.Player.MobIsSpawned(Mob.KorrolokaTunnel.KORROLOKA_LEECH, offset))
                {
                    return true;
                }
            }

            return false;
        }

        // isNMDefeated: one of the three is dead
        static bool IsNMDefeated(IQuestContext q)
        {
            for (int offset = 0; offset <= 2; offset++)
            {
                if (q.Player.MobIsDead(Mob.KorrolokaTunnel.KORROLOKA_LEECH, offset))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                KeyItem = KeyItem.JobGestureNinja,
                Title = Title.ShadowWalker,
            });

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.PortBastok, Npc.PortBastok.Kaede).OnTrigger(QuestReply.ProgressEvent(240))
                .OnEventFinish(Zone.PortBastok, 240, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel.qm2).OnTrigger(q =>
                {
                    if (!IsNMSpawned(q) && q.Quest.Step == 2)
                    {
                        if (q.Quest.LocalVar("nmDefeated") == 1)
                        {
                            q.Quest.SetStep(3);

                            if (q.Quest.LocalVar("isActor") == 1)
                            {
                                q.Quest.SetLocalVar("isActor", 0);
                                q.Npc.Hide(q.Settings.Main("FORCE_SPAWN_QM_RESET_TIME"));
                            }

                            return QuestReply.KeyItem(KeyItem.StrangelyShapedCoral);
                        }
                        else
                        {
                            q.Quest.SetLocalVar("isActor", 1);

                            for (int offset = 0; offset <= 2; offset++)
                            {
                                // SpawnMob(nmId):updateClaim(player)
                                q.Player.SpawnMob(Mob.KorrolokaTunnel.KORROLOKA_LEECH, offset, claim: true);
                            }

                            return QuestReply.MessageText(Text.KorrolokaTunnel.HAIR_STANDS_ON_END);
                        }
                    }
                    return null;
                })
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel.KorrolokaLeech).OnMobDeath(q =>
                {
                    if (IsNMDefeated(q) && q.Quest.Step == 2)
                    {
                        q.Quest.SetLocalVar("nmDefeated", 1);
                    }
                })
                .Npc(Zone.Norg, Npc.Norg.Ryoma).OnTrigger(q =>
                    q.Quest.Step == 4 ? QuestReply.ProgressEvent(95) : null)
                .OnEventFinish(Zone.Norg, 95, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SealedDagger);
                    q.Player.DelKeyItem(KeyItem.StrangelyShapedCoral);
                    q.Quest.SetStep(5);
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Ensetsu).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress >= 1 && questProgress <= 2)
                    {
                        return QuestReply.ProgressEvent(242);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(245);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.Event(243);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(246, KeyItem.SealedDagger);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Kagetora).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(241);
                    }
                    else if (questProgress > 2)
                    {
                        return QuestReply.Event(244);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 241, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.PortBastok, 242, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.PortBastok, 245, q => q.Quest.SetStep(4))
                .OnEventFinish(Zone.PortBastok, 246, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SealedDagger);
                        q.Player.UnlockJob(Job.Nin);
                        q.Text.Special(Text.PortBastok.UNLOCK_NINJA);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Kaede).OnTrigger(QuestReply.Event(248).ReplaceDefault());
        }
    }
}
