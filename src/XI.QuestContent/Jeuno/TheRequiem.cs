// The Requiem (Log ID 3, Quest ID 64), a line-for-line port of scripts/quests/jeuno/The_Requiem.lua (quest rework B3b).
// Bki Tbujhja : !pos -22 0 -60 245; Mataligeat : !pos -24 0 -60 245; Sarcophagus : !pos -421 6.5 503 195. BRD AF2:
// Painful Memory completed, a bard at AF2_QUEST_LEVEL. Bki Tbujhja offers 145 (option 1 begins it, option 0 sets
// Option 1) then 148 (option 1 begins it). Accepted: a Flask of Holy Water traded alone to Bki at Prog 0 plays 151
// (blessed and handed back: not taken; option 0: Prog 1); Bki answers 146 at Prog 0, else 147 or 149 (a 50 % roll), and
// 150 once Star Ring 1 is held (completed: Choral Slippers, fame +7 x3); Mataligeat 142 from Prog 1 without the ring.
// The bard's sarcophagus (SARCOPHAGUS_OFFSET + 3) takes the flask at Prog 1 before the guardians were popped (Yum Kimil
// and the next two mobs; the flask taken, Popped 1; noAction), pops them again on a trigger after a wipe, and plays 46
// at Prog 2 (Yum Kimil's death: option 0 gives Star Ring 1, kept after completion). Completed: Bki 134
// (replaceDefault).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheRequiem : QuestScript
    {
        public TheRequiem() : base(QuestLog.Jeuno, "THE_REQUIEM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Requiem";

        // ID.npc.SARCOPHAGUS_OFFSET + 3: the bard's sarcophagus, the fourth of the five
        static bool IsBardsSarcophagus(IQuestContext q) =>
            q.Npc.Id == q.Player.NpcIdOf("SARCOPHAGUS_OFFSET") + 3;

        // npcUtil.popFromQM(player, npc, { ID.mob.YUM_KIMIL, ID.mob.YUM_KIMIL + 1, ID.mob.YUM_KIMIL + 2 }, { hide = 0 })
        static bool PopGuardians(IQuestContext q) =>
            q.Player.PopFromQmSeries("YUM_KIMIL", 3, 0);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.ChoralSlippers });
            b.Steps(1, 2);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "PAINFUL_MEMORY") &&
                    q.Player.MainJob == Job.Brd &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(145);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(148); // Short offer after the player declined once.
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 145, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else if (q.Option == 0)
                    {
                        q.Quest.SetVar("Option", 1); // Player declined.
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 148, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrade(q =>
                {
                    if (q.Quest.Step == 0 && q.Trade.Matches((Item.FlaskOfHolyWater, 1)))
                    {
                        return QuestReply.ProgressEvent(151); // Bki blesses the flask and hands it back.
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StarRing1))
                    {
                        return QuestReply.ProgressEvent(150);
                    }
                    else if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(146);
                    }
                    else if (q.Player.RandomInt(1, 100) <= 50)
                    {
                        return QuestReply.Event(147);
                    }
                    else
                    {
                        return QuestReply.Event(149);
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mataligeat).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 1 && !q.Player.HasKeyItem(KeyItem.StarRing1))
                    {
                        return QuestReply.Event(142);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 150, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 151, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Sarcophagus).OnTrade(q =>
                {
                    if (IsBardsSarcophagus(q) &&
                        q.Quest.Step == 1 &&
                        q.Quest.Var("Popped") == 0 &&
                        q.Trade.Matches((Item.FlaskOfHolyWater, 1)) &&
                        PopGuardians(q))
                    {
                        q.Trade.Complete();
                        q.Quest.SetVar("Popped", 1);
                        return QuestReply.NoAction(); // No message. The guardians spawn and immediately aggro.
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (!IsBardsSarcophagus(q))
                    {
                        return null;
                    }

                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(46);
                    }
                    else if (q.Quest.Step == 1 && q.Quest.Var("Popped") == 1 && PopGuardians(q))
                    {
                        return QuestReply.NoAction(); // Repop after a wipe. No message.
                    }
                    return null;
                })
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.YumKimil).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.TheEldiemeNecropolis, 46, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.StarRing1); // Kept after completion. The Circle of Time consumes it.
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrigger(QuestReply.Event(134).ReplaceDefault());
        }
    }
}
