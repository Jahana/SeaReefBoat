// A New Dawn (Log ID 3, Quest ID 62; BST AF3), ported from the A New Dawn branches of
// scripts/zones/Lower_Jeuno/npcs/_6t2.lua (the door "Merchant's House"), scripts/zones/Upper_Jeuno/npcs/Osker.lua,
// scripts/zones/The_Eldieme_Necropolis/npcs/Sarcophagus.lua and the death of
// scripts/zones/The_Eldieme_Necropolis/mobs/Sturm.lua (quest rework B3b, Jeuno group 19; call-only: each script now calls
// this quest at the branches' spot). _6t2 : !pos -82 -7 -169 245; Osker : !pos -61 8 94 244; Sarcophagus :
// !pos -420 8 500 195.
// The door _6t2: Scattered into Shadow completed and this quest available: a BST of main level AF3_QUEST_LEVEL or
// more: the var ANewDawn_Event 0: 5 (its finish: the var 1; option 1: begun, the var 2); 1: 4 (its finish, option 1:
// begun, the var 2); any other var: nothing; not such a BST: 1. Accepted: the var 2: 2; 4 or more: 3; else nothing.
// Completed with the var 6: 0 (its finish: the var 0). Else nothing (the door's default plays).
// Osker: accepted: the var 2 or 3: 146; 4 or more: 147; else nothing. Completed: 145. His trade (the Mahogany Lumber
// alone at the var 3: the trade taken, 148; its finish the Tamer's Whistle and the var 4) and 146's finish (the var 2 ->
// 3) read only the var: they stay in his script.
// The Sarcophagus, accepted and the first one (zones[zone].npc.SARCOPHAGUS_OFFSET): the var 4: Sturm, Taifun and Trombe
// popped (npcUtil.popFromQM, claim false, hide 0; nothing shown); 5: 45 (its finish: npcUtil.completeQuest step for
// step, the Beast Trousers (stopping when they cannot be given) and Paragon of Beastmaster Excellence, then the raw
// player:completeQuest, which keeps the quest's own Quest[3][62] vars; completed: the var 6, the Tamer's Whistle
// taken); any other var: nothing (no message). Not accepted, or another Sarcophagus: its script's
// SARCOPHAGUS_CANNOT_BE_OPENED.
// Sturm's death, accepted with the var 4: the var 5 (for each char the mob script runs it for). The despawn of Taifun
// and Trombe stays in its script.
// Kept as the Lua has it: 5's finish sets the var 1 on any option, then 2 on option 1; 4 begins on option 1 only; no
// finish reads the status (45 completes whatever the status); Osker plays nothing at an accepted var 0 / 1; the door's
// 0 replays while the var stays 6 (its finish clears it).
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): with no killer (optParams.noKiller) the Lua's
// onMobDeath indexed a nil player and raised before the despawns, so Taifun and Trombe stayed up; Sturm.lua now calls
// this quest only with a player, and the despawns run.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): _6t2.lua reads the literal 50, the C#
// reads AF3_QUEST_LEVEL as the other AF3 quests do.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ANewDawn : QuestScript
    {
        const string Var = "ANewDawn_Event";

        public ANewDawn() : base(QuestLog.Jeuno, "A_NEW_DAWN") { }

        // _6t2.lua: player:getQuestStatus(JEUNO, SCATTERED_INTO_SHADOW) == QUEST_COMPLETED
        static bool ScatteredIntoShadowCompleted(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Jeuno, "SCATTERED_INTO_SHADOW") == QuestStatus.Completed;

        // _6t2.lua: player:getMainJob() == xi.job.BST and mLvl >= 50 (AF3_QUEST_LEVEL here: see the header)
        static bool BstAf3Level(IQuestCheck q) =>
            q.Player.MainJob == Job.Bst && q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL");

        // Sarcophagus.lua: npc:getID() == ID.npc.SARCOPHAGUS_OFFSET (the first of the zone's Sarcophagi)
        static bool IsFirstSarcophagus(IQuestContext q) => q.Npc.Id == q.Player.NpcIdOf("SARCOPHAGUS_OFFSET");

        protected override void Build(QuestScriptBuilder b)
        {
            // _6t2.lua onTrigger: Scattered into Shadow completed, available, a BST 50+ with the var 0
            b.State(QuestStatus.Available, when: q =>
                    ScatteredIntoShadowCompleted(q) && BstAf3Level(q) && q.Player.CharVar(Var) == 0)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(5));

            // the var 1
            b.State(QuestStatus.Available, when: q =>
                    ScatteredIntoShadowCompleted(q) && BstAf3Level(q) && q.Player.CharVar(Var) == 1)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(4));

            // not a BST 50+
            b.State(QuestStatus.Available, when: q => ScatteredIntoShadowCompleted(q) && !BstAf3Level(q))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(1));

            // accepted, the var 2
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(2));

            // accepted, the var 4 or more
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) >= 4)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(3));

            // completed, the var 6
            b.State(QuestStatus.Completed, when: q => q.Player.CharVar(Var) == 6)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).Called().OnTrigger(QuestReply.Event(0));

            // Osker.lua onTrigger: accepted, the var 2 or 3
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2 || q.Player.CharVar(Var) == 3)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).Called().OnTrigger(QuestReply.Event(146));

            // accepted, the var 4 or more
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) >= 4)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).Called().OnTrigger(QuestReply.Event(147));

            // completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).Called().OnTrigger(QuestReply.Event(145));

            // Sarcophagus.lua onTrigger, A NEW DAWN: accepted, the first Sarcophagus, the var 4: npcUtil.popFromQM(player, npc,
            // { ID.mob.STURM, ID.mob.TAIFUN, ID.mob.TROMBE }, { claim = false, hide = 0 }) (the three ids are consecutive:
            // the series of 3 from STURM); nothing shown. Another Sarcophagus: its script's message
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 4)
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Sarcophagus).Called().OnTrigger(q =>
                {
                    if (!IsFirstSarcophagus(q))
                    {
                        return null;
                    }
                    q.Player.PopFromQmSeries(Mob.TheEldiemeNecropolis.STURM, 3, hide: 0, claim: false, look: false);
                    return QuestReply.NoAction();
                });

            // the var 5: 45
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 5)
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Sarcophagus).Called().OnTrigger(q =>
                    IsFirstSarcophagus(q) ? QuestReply.Event(45) : null);

            // any other var: nothing (no message)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Sarcophagus).Called().OnTrigger(q =>
                    IsFirstSarcophagus(q) ? QuestReply.NoAction() : null);

            // Sturm.lua onMobDeath: accepted, the var 4
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 4)
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Sturm).Called().OnMobDeath(q =>
                    q.Player.SetCharVar(Var, 5));

            b.AnyState()
                // _6t2.lua onEventFinish 5 / 4 / 0
                .OnEventFinish(Zone.LowerJeuno, 5, q =>
                {
                    q.Player.SetCharVar(Var, 1);
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Var, 2);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 4, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Var, 2);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 0, q => q.Player.SetCharVar(Var, 0))
                // Sarcophagus.lua onEventFinish 45: npcUtil.completeQuest with the reward
                .OnEventFinish(Zone.TheEldiemeNecropolis, 45, q =>
                {
                    if (CompleteWithReward(q))
                    {
                        q.Player.SetCharVar(Var, 6);
                        q.Player.DelKeyItem(KeyItem.TamersWhistle);
                    }
                });
        }

        // npcUtil.completeQuest(player, JEUNO, A_NEW_DAWN, { item = 14222, title = PARAGON_OF_BEASTMASTER_EXCELLENCE })
        // step for step: not quest:complete, whose cleanup would clear the quest's own Quest[3][62] vars
        static bool CompleteWithReward(IQuestContext q)
        {
            if (!q.Player.GiveItem(Item.BeastTrousers))
            {
                return false; // no free slot (or the trousers held): npcUtil.completeQuest returns false
            }

            q.Player.AddTitle(Title.ParagonOfBeastmasterExcellence);
            q.Player.CompleteQuest(QuestLog.Jeuno, "A_NEW_DAWN");
            return true;
        }
    }
}
