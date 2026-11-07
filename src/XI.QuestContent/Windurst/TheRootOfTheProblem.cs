// The Root of the Problem (Log ID 2, Quest ID 68; BLM AF3), ported from the The Root of the Problem branches of
// scripts/zones/Heavens_Tower/npcs/Chumimi.lua and scripts/zones/Windurst_Walls/npcs/Koru-Moru.lua (quest rework B3b,
// Windurst group 30; call-only). Chumimi : !pos 0.1 30 21 242; Koru-Moru : !pos -120 -6 124 239.
// Chumimi, after The Three Magi's and Recollections' arms (their own C#, which her script calls first): Recollections
// completed, this quest available, BLM, main level >= AF3_QUEST_LEVEL, no needToZone: 276 (0, Square of Silk Cloth);
// its finish, any option: begun, rootProblem 1. Accepted: the var 1: 277 (0, Square of Silk Cloth); 2: 279 (its finish:
// Sluice Surveyor MK I, npcUtil.giveKeyItem); 3: 281 (its finish: npcUtil.giveItem (Wizard's Petasos); given:
// completed, Windurst fame 20, Paragon of Black Mage Excellence, the key item taken); any other var:
// nothing (no event, and her 259 not played). Else her 259 (her script). The trade, accepted and the var 1: one
// Square of Silk Cloth and nothing else: 278 (no finish: nothing happens).
// Koru-Moru, after his Carbuncle Debacle and Class Reunion arms (they stay in his script): accepted and the var 1:
// 348 (0, Square of Silk Cloth); else nothing. His trade: his script checks one Square of Silk Cloth, nothing else and
// no gil, then calls this quest: accepted: 349 started, then the trade taken and the var 2 (all in onTrade).
// His default 193 (DefaultActions) is a Default-priority action, so the lookup takes turns with his script (the
// default's turn, the script's turn, ...). Star Struck, Blast from the Past, Making the Grade and
// The Puppet Master (their containers) answer him first with their Progress arms; their Event arms take turns with his
// script (Star Struck's completed 213 replaces the default). Riding on the Clouds' trade acts on a Kindred's Seal only.
// The var's 2 -> 3 (Toraimarai Canal's ??? and Magic Sludge) never names the quest: it stays in Lua.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 276's level is AF3_QUEST_LEVEL, as the
// other AF3 offers read it; the Lua's is the literal 50 (the setting's default).
// Kept as the Lua has it: 278 has no finish (the silk stays, the var stays 1); the var is never cleared at the
// completion; 276 begins on any option; 279 plays again while the var is 2, its finish calling npcUtil.giveKeyItem
// again; 349 reads no var (var 3 goes back to 2); the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheRootOfTheProblem : QuestScript
    {
        const string Var = "rootProblem";

        public TheRootOfTheProblem() : base(QuestLog.Windurst, "THE_ROOT_OF_THE_PROBLEM") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Chumimi).Called()
                    // Chumimi.lua onTrade, the The Root of the Problem arm
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Player.CharVar(Var) == 1 &&
                            q.Trade.HasItemQty(Item.SquareOfSilkCloth, 1) &&
                            q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(278);
                        }

                        return null;
                    })
                    // Chumimi.lua onTrigger, the The Root of the Problem arms (her 259 stays in her script)
                    .OnTrigger(q =>
                    {
                        QuestStatus recollections = q.Player.QuestStatus(QuestLog.Windurst, "RECOLLECTIONS");
                        QuestStatus rootProblem = q.Quest.Status;
                        int mLvl = q.Player.MainLevel;
                        int mJob = q.Player.MainJob;

                        if (recollections == QuestStatus.Completed &&
                            rootProblem == QuestStatus.Available &&
                            mJob == Job.Blm &&
                            // the Lua's literal 50 (fixed, user ruling 2026-11-04)
                            mLvl >= q.Settings.MainNumber("AF3_QUEST_LEVEL") &&
                            !q.Player.NeedToZone)
                        {
                            // Start Quest "The Root of The problem"
                            return QuestReply.Event(276, 0, Item.SquareOfSilkCloth);
                        }
                        else if (rootProblem == QuestStatus.Accepted)
                        {
                            int rootProblemCS = q.Player.CharVar(Var);

                            if (rootProblemCS == 1)
                            {
                                return QuestReply.Event(277, 0, Item.SquareOfSilkCloth);
                            }
                            else if (rootProblemCS == 2)
                            {
                                return QuestReply.Event(279);
                            }
                            else if (rootProblemCS == 3)
                            {
                                return QuestReply.Event(281);
                            }

                            return QuestReply.NoAction(); // the arm taken, no event: her 259 not played
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).Called()
                    // Koru-Moru.lua onTrade, the silk cloth arm (his script checks the trade, then calls)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            // player:startEvent, then player:tradeComplete and the var: all at the trade
                            q.Player.StartEvent(349);
                            q.Trade.Complete();
                            q.Player.SetCharVar(Var, 2);
                        }

                        return null;
                    })
                    // Koru-Moru.lua onTrigger, the last arm
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Var) == 1)
                        {
                            return QuestReply.Event(348, 0, Item.SquareOfSilkCloth);
                        }

                        return null;
                    });

            // Chumimi.lua onEventFinish, the The Root of the Problem arms
            b.AnyState()
                .OnEventFinish(Zone.HeavensTower, 276, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(Var, 1);
                })
                .OnEventFinish(Zone.HeavensTower, 279, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SluiceSurveyorMkI);
                })
                .OnEventFinish(Zone.HeavensTower, 281, q =>
                {
                    if (q.Player.GiveItem(Item.WizardsPetasos))
                    {
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][68] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "THE_ROOT_OF_THE_PROBLEM");
                        q.Player.AddFame(FameArea.Windurst, 20);
                        q.Player.AddTitle(Title.ParagonOfBlackMageExcellence);
                        q.Player.DelKeyItem(KeyItem.SluiceSurveyorMkI);
                    }
                });
        }
    }
}
