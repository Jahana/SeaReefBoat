// Comeback Queen (Log ID 3, Quest ID 98), a line-for-line port of scripts/quests/jeuno/DNC_AF3_Comeback_Queen.lua
// (quest rework B3b). Laila : !pos -54.045 -1 100.996 244; Rhea Myuliah : !pos -56.220 -1 101.805 244; Harmodios : !pos
// -79.928 -4.824 -135.114 235. A Dancer from AF3_QUEST_LEVEL with The Road to Divadom completed, its mustZone char var
// clear and Timer reached: Laila offers 10143 (begun, Wyatt's Proposal). Accepted: Harmodios in Bastok Markets 490 at
// Prog 0 (the proposal taken, Prog 1), else 491 once per zone; Laila 10144 at Prog 0, 10147 at 1 (Timer = tomorrow,
// Prog 2), at Prog <= 3 (2, 3, and any GM's Prog below 0) 10148 + 4 * (Prog - 2) before Timer, else 10151 with Prog - 2
// (option 0: Timer = tomorrow, mustZone, Prog 3; any other: Prog + 1 and the char set down where it stands, a zone-in),
// 10154 at Prog 8; Olgald 10146 at Prog 0, 10150 to Prog 2, 10153 at 3, all at priority 995 (over the crafted Dancer
// artifact's 10168, under its 10167); Rhea Myuliah 10145 to Prog 1, 10149 to 3; entering Upper Jeuno at Prog 4-7 plays
// 10208-10211 (each: Prog + 1 and a zone-in; 10211 and 10154: the Dancer's Casaque for the char's gender, completed,
// Elegant Dancer, A Furious Finale's Timer = tomorrow and the local var Quest[3][99]mustZone; 10211 with no room: Prog
// 8). Completed: six Upper Jeuno NPCs' events over their defaults.
// Kept Lua quirks: 10151 answered with anything but 0 jumps a step, even at Prog 2; the accepted section never reads
// the mustZone 10151's option 0 sets; Laila's completed 10154 has no finish; a Prog below 0 takes the "<=" arms (Laila
// 10148 + 4 * (Prog - 2), or 10151 with Prog - 2; Olgald 10150; Rhea 10145); the finishes write A Furious Finale's
// mustZone as a local var (player:setLocalVar), which getMustZone (the char var) never reads.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ComebackQueen : QuestScript
    {
        public ComebackQueen() : base(QuestLog.Jeuno, "COMEBACK_QUEEN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/DNC_AF3_Comeback_Queen";

        // The final series of events for this quest involve several onZoneIn cutscenes back to back; however,
        // the player doesn't move.  Increment quest var with this function, and force a zone in their current
        // position.  This also ensures that things will resume where left off if the player disconnects.
        static void ProgressZone(IQuestContext q)
        {
            var currentPos = q.Player.Pos;

            q.Quest.IncrementVar("Prog", 1);
            q.Player.SetPos(currentPos.X, currentPos.Y, currentPos.Z, currentPos.Rot, Zone.UpperJeuno);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.ElegantDancer });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Dnc &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL") &&
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_ROAD_TO_DIVADOM") &&
                    !q.Quest.MustZone &&
                    q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.ProgressEvent(10143))
                .OnEventFinish(Zone.UpperJeuno, 10143, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.WyattsProposal);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(10144);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(10147);
                    }
                    else if (questProgress <= 3)
                    {
                        int progressOffset = questProgress - 2;

                        if (q.Player.VanadielUniqueDay < q.Quest.Var("Timer"))
                        {
                            // Timer active - First time: 10148, Repeat: 10152
                            return QuestReply.ProgressEvent(10148 + (4 * progressOffset));
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10151, progressOffset);
                        }
                    }
                    else if (questProgress == 8)
                    {
                        return QuestReply.ProgressEvent(10154);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Olgald).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        // Blocking event after the Dancer Tailor series (10167 and 10168), for those quests
                        // 10167 is a progressEvent, 10168 is oncePerZone()

                        return QuestReply.Event(10146).SetPriority(995);
                    }
                    else if (questProgress <= 2)
                    {
                        return QuestReply.Event(10150).SetPriority(995);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(10153).SetPriority(995);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress <= 1)
                    {
                        return QuestReply.ProgressEvent(10145);
                    }
                    else if (questProgress <= 3)
                    {
                        return QuestReply.ProgressEvent(10149);
                    }
                    return null;
                })
                .OnZoneIn(Zone.UpperJeuno, q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4)
                    {
                        return QuestReply.Event(10208);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.Event(10209);
                    }
                    else if (questProgress == 6)
                    {
                        return QuestReply.Event(10210);
                    }
                    else if (questProgress == 7)
                    {
                        return QuestReply.Event(10211);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 10147, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.UpperJeuno, 10151, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                        q.Quest.SetMustZone();
                        q.Quest.SetStep(3);
                    }
                    else
                    {
                        ProgressZone(q);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10154, q =>
                {
                    int dancersCasaque = Item.DancersCasaqueF - q.Player.Gender;

                    if (q.Player.GiveItem(dancersCasaque))
                    {
                        q.Quest.Complete();

                        q.Player.SetCharVar("Quest[3][99]Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetLocalVar("Quest[3][99]mustZone", 1);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10208, q =>
                {
                    ProgressZone(q);
                })
                .OnEventFinish(Zone.UpperJeuno, 10209, q =>
                {
                    ProgressZone(q);
                })
                .OnEventFinish(Zone.UpperJeuno, 10210, q =>
                {
                    ProgressZone(q);
                })
                .OnEventFinish(Zone.UpperJeuno, 10211, q =>
                {
                    int dancersCasaque = Item.DancersCasaqueF - q.Player.Gender;

                    if (q.Player.GiveItem(dancersCasaque))
                    {
                        q.Quest.Complete();

                        q.Player.SetCharVar("Quest[3][99]Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetLocalVar("Quest[3][99]mustZone", 1);
                    }
                    else
                    {
                        q.Quest.SetStep(8);
                    }
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Harmodios).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(490);
                    }
                    else
                    {
                        return QuestReply.Event(491).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 490, q =>
                {
                    q.Player.DelKeyItem(KeyItem.WyattsProposal);
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Completed)
                // TODO: This condition may change with the implementation of
                // Dancer Limit Break
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Alfrieda).OnTrigger(QuestReply.Event(10157).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Finnela).OnTrigger(QuestReply.Event(10159).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.Event(10154).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Olgald).OnTrigger(QuestReply.Event(10156).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(QuestReply.Event(10155).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Turlough).OnTrigger(QuestReply.Event(10158).ReplaceDefault());
        }
    }
}
