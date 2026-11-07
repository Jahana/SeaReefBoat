// The Road to Divadom (Log ID 3, Quest ID 97), a line-for-line port of
// scripts/quests/jeuno/DNC_AF2_The_Road_to_Divadom.lua (quest rework B3b). Laila : !pos -54.045 -1 100.996 244; Rhea
// Myuliah : !pos -56.220 -1 101.805 244; Glowing Pebbles : !pos 104.2 4.1 443.6 82. A Dancer from AF2_QUEST_LEVEL with
// The Unfinished Waltz completed, its mustZone char var clear and Timer reached: Laila offers 10136 (begun). Accepted,
// as a Dancer: Laila 10137 at Prog 0; at Prog 3 she starts 10139 (10214 and 10215 queued) and plays 10170, at Prog 4
// 10170 alone (the Dancer's Tights for the char's gender: completed, Stardust Dancer, Comeback Queen's Timer = tomorrow
// by its var name, the local var Quest[3][98]mustZone, the crafted artifact's HQuest[DncArtifact]Prog 1; no room: Prog
// 4); Rhea Myuliah 10138; entering Jugner Forest [S] at Prog 0 plays 105 (Prog 1); the Glowing Pebbles 106 at Prog 1
// (Prog 2) and, at Prog 2, a Block of Yagudo Glue alone 107 (the trade confirmed, Prog 3). Completed, until Comeback
// Queen is completed: Laila 10140, Rhea Myuliah 10141, over the default.
// Kept Lua quirks: the accepted section needs a Dancer (a char that changes job loses every reply of the quest, and its
// Jugner Forest [S] zone-in); 10170 writes Comeback Queen's mustZone as a local var (player:setLocalVar), which
// getMustZone (the char var) never reads.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheRoadToDivadom : QuestScript
    {
        public TheRoadToDivadom() : base(QuestLog.Jeuno, "THE_ROAD_TO_DIVADOM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/DNC_AF2_The_Road_to_Divadom";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.StardustDancer });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_UNFINISHED_WALTZ") &&
                    q.Player.MainJob == Job.Dnc &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                    !q.Quest.MustZone &&
                    q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.ProgressEvent(10136))
                .OnEventFinish(Zone.UpperJeuno, 10136, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.MainJob == Job.Dnc)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(10137);
                    }
                    else if (questProgress == 3)
                    {
                        // Note: Retail handles this as a progression of onZoneIn events.
                        // Instead, we queue these here.

                        q.Player.StartEvent(10139);
                        q.Player.StartEvent(10214);
                        q.Player.StartEvent(10215);
                        return QuestReply.ProgressEvent(10170);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(10170);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(QuestReply.Event(10138))
                .OnEventFinish(Zone.UpperJeuno, 10170, q =>
                {
                    int tightsItemID = Item.DancersTightsF - q.Player.Gender;

                    if (q.Player.GiveItem(tightsItemID))
                    {
                        q.Quest.Complete();

                        q.Player.SetCharVar("Quest[3][98]Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetLocalVar("Quest[3][98]mustZone", 1);
                        q.Player.SetCharVar("HQuest[DncArtifact]Prog", 1);
                    }
                    else
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.GlowingPebbles).OnTrade(q =>
                {
                    if (q.Quest.Step == 2 &&
                        q.Trade.HasExactly(Item.BlockOfYagudoGlue))
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(106);
                    }
                    return null;
                })
                .OnZoneIn(Zone.JugnerForestS, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(105);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 105, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.JugnerForestS, 106, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.JugnerForestS, 107, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Player.HasCompletedQuest(QuestLog.Jeuno, "COMEBACK_QUEEN"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.Event(10140).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(QuestReply.Event(10141).ReplaceDefault());
        }
    }
}
