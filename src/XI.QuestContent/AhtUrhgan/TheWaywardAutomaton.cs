// The Wayward Automaton (Log ID 6), ported from the quest branches of
// scripts/zones/Aht_Urhgan_Whitegate/npcs/Iruki-Waraki.lua, scripts/zones/Nashmau/npcs/Dnegan.lua,
// scripts/zones/Caedarva_Mire/npcs/qm9.lua and the death of scripts/zones/Caedarva_Mire/mobs/Caedarva_Toad.lua (quest
// rework B3b, Aht Urhgan group 11; call-only: the scripts now call this quest). Iruki-Waraki : !pos 101.329 -6.999
// -29.042 50; Dnegan : !pos 29.89 -6 55.83 53; qm9 : !pos 129 1.396 -631 79.
// Iruki-Waraki, the first arms of his talk: a PUP of AF1_QUEST_LEVEL with No Strings Attached completed and this quest
// available: 774 (its finish: TheWaywardAutomatonProgress 1, begun, any option); accepted at progress 1: 775; at
// progress 3: 776 (its finish: npcUtil.completeQuest with the Turbo Animator, the var cleared); completed: 777 for a
// PUP below AF2_QUEST_LEVEL or any other job. Else his script goes on (267, then Operation Teatime's arms).
// Dnegan, first: accepted at progress 1: 289; progress 2 (any status): 289 (its finish: progress 2). Else Operation
// Teatime's arm, then his 288.
// qm9: accepted at progress 2: TheWaywardAutomatonNM 1 or more: 14 (its finish: progress 3, the NM var 0); else the
// Caedarva Toad not spawned: HIDEOUS_BEAST_EMERGES and the toad spawned and claimed (SpawnMob():updateClaim(player));
// else (the toad up) nothing. Not at progress 2 or not accepted: SOMEONE_SLIPPED.
// The Caedarva Toad's death: accepted at progress 2 with the NM var 0: the NM var 1.
// Kept as the Lua has it: 774's and 776's finishes read no option and no status; Dnegan's progress-2 arm reads no
// status, and its 289 finish sets progress 2 in any status and for any option; qm9's 14 finish reads no status; the
// toad's death counts for any char that gets the call (the killer's alliance, as onMobDeath runs). A toad death with
// no killer: the Lua raised at player:getQuestStatus (nil player); the call returns false, so only the error log
// line is gone (the state is the same).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class TheWaywardAutomaton : QuestScript
    {
        const string Prog = "TheWaywardAutomatonProgress";
        const string Nm = "TheWaywardAutomatonNM";

        public TheWaywardAutomaton() : base(QuestLog.AhtUrghan, "THE_WAYWARD_AUTOMATON") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Iruki-Waraki.lua onTrigger: the Wayward Automaton arms, first in his chain
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).Called().OnTrigger(q =>
                {
                    QuestStatus noStringsAttached = q.Player.QuestStatus(QuestLog.AhtUrghan, "NO_STRINGS_ATTACHED");
                    QuestStatus theWaywardAutomaton = q.Quest.Status;
                    int progress = q.Player.CharVar(Prog);
                    int playerLvl = q.Player.MainLevel;
                    int playerJob = q.Player.MainJob;

                    if (playerJob == Job.Pup &&
                        playerLvl >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                        noStringsAttached == QuestStatus.Completed &&
                        theWaywardAutomaton == QuestStatus.Available)
                    {
                        return QuestReply.Event(774); // he tells you to help find his automaton
                    }
                    else if (theWaywardAutomaton == QuestStatus.Accepted && progress == 1)
                    {
                        return QuestReply.Event(775); // reminder to head to Nashmau
                    }
                    else if (theWaywardAutomaton == QuestStatus.Accepted && progress == 3)
                    {
                        return QuestReply.Event(776); // tell him you found the automaton
                    }
                    else if (playerJob == Job.Pup &&
                        playerLvl < q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                        theWaywardAutomaton == QuestStatus.Completed)
                    {
                        return QuestReply.Event(777);
                    }
                    else if (playerJob != Job.Pup &&
                        theWaywardAutomaton == QuestStatus.Completed)
                    {
                        return QuestReply.Event(777);
                    }

                    return null;
                })
                // Dnegan.lua onTrigger: the Wayward Automaton arms, first in his chain
                .Npc(Zone.Nashmau, Npc.Nashmau.Dnegan).Called().OnTrigger(q =>
                {
                    int progress = q.Player.CharVar(Prog);

                    if (q.Quest.Status == QuestStatus.Accepted && progress == 1)
                    {
                        return QuestReply.Event(289); // he tells you to go to Caedarva Mire
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(289); // hint to go to Caedarva Mire
                    }

                    return null;
                })
                // qm9.lua onTrigger
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.qm9).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Prog) == 2)
                    {
                        if (q.Player.CharVar(Nm) >= 1)
                        {
                            return QuestReply.Event(14); // the cutscene after the toad
                        }
                        else if (!q.Player.MobIsSpawned(Mob.CaedarvaMire.CAEDARVA_TOAD))
                        {
                            q.Text.Special(Text.CaedarvaMire.HIDEOUS_BEAST_EMERGES);
                            // SpawnMob(ID.mob.CAEDARVA_TOAD):updateClaim(player)
                            q.Player.SpawnMob(Mob.CaedarvaMire.CAEDARVA_TOAD, 0, claim: true);
                            return QuestReply.NoAction();
                        }

                        return null;
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.CaedarvaMire.SOMEONE_SLIPPED);
                    }
                });

            // Caedarva_Toad.lua onMobDeath
            b.State(QuestStatus.Accepted)
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.CaedarvaToad).Called().OnMobDeath(q =>
                {
                    if (q.Player.CharVar(Prog) == 2 &&
                        q.Player.CharVar(Nm) == 0)
                    {
                        q.Player.SetCharVar(Nm, 1);
                    }
                });

            // the scripts' onEventFinish: Iruki-Waraki 774 / 776, Dnegan 289, qm9 14
            b.AnyState()
                .OnEventFinish(Zone.AhtUrhganWhitegate, 774, q =>
                {
                    q.Player.SetCharVar(Prog, 1);
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 776, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[6][27] vars
                    if (q.Player.GiveItem(Item.TurboAnimator))
                    {
                        q.Player.SetCharVar(Prog, 0);
                        q.Player.CompleteQuest(QuestLog.AhtUrghan, "THE_WAYWARD_AUTOMATON");
                    }
                })
                .OnEventFinish(Zone.Nashmau, 289, q =>
                {
                    q.Player.SetCharVar(Prog, 2);
                })
                .OnEventFinish(Zone.CaedarvaMire, 14, q =>
                {
                    q.Player.SetCharVar(Prog, 3);
                    q.Player.SetCharVar(Nm, 0);
                });
        }
    }
}
