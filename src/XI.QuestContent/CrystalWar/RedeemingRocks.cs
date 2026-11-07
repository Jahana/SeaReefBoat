// Redeeming Rocks (Log ID 7, Quest ID 4), ported from the quest branches of scripts/zones/Windurst_Waters_[S]/npcs/
// Kocco_Ehllek.lua and Rohn_Ehlbalna.lua and scripts/zones/Vunkerl_Inlet_[S]/npcs/qm4.lua (quest rework B3b Crystal
// War, draft; call-only). Kocco Ehllek !pos -41.465 -1.125 -163.018 94; Rohn Ehlbalna !pos -43.473 -3.5 46.496 94; qm4
// !pos -413.083 -28.953 -45.12 83. The char vars RedeemingRocksProg and RedeemingDay (no quest prefix).
// Kocco, in his order: available: 108 (finish: the quest added, Prog 1); accepted, Prog 2: 109 (-> 3); accepted, Prog
// 4: 110 (-> Prog 0, RedeemingDay today); accepted, RedeemingDay not today: 111 (finish: npcUtil.completeQuest:
// Kocco's Earring, RedeemingDay deleted); else his script's 140. Rohn: accepted, Prog 1: 114 (-> 2); else his
// script's 440. qm4: Prog 3 (any status: `getQuestStatus(...) and` is always true in Lua): the Piece of Kionite, Prog
// 4; else its script's NOTHING_OUT_OF_ORDINARY.
// Kept as the Lua has it: accepted at Prog 1 or 3 with RedeemingDay 0 (never set yet) is "not today": Kocco's 111
// completes the quest before the Kionite (the char needs Rohn's 114 first only if it talks to Rohn first); qm4 reads
// no status; 110 writes Prog 0 (a delete); the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class RedeemingRocks : QuestScript
    {
        public RedeemingRocks() : base(QuestLog.CrystalWar, "REDEEMING_ROCKS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Kocco_Ehllek.lua onTrigger, in its order
            b.AnyState()
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.KoccoEhllek).Called().OnTrigger(q =>
                {
                    QuestStatus redeemingRocks = q.Quest.Status;
                    int rocksProg = q.Player.CharVar("RedeemingRocksProg");

                    if (redeemingRocks == QuestStatus.Available)
                    {
                        return QuestReply.Event(108); // Start quest "Redeeming Rocks"
                    }
                    else if (redeemingRocks == QuestStatus.Accepted && rocksProg == 2)
                    {
                        return QuestReply.Event(109); // 3rd CS quest "Redeeming Rocks"
                    }
                    else if (redeemingRocks == QuestStatus.Accepted && rocksProg == 4)
                    {
                        return QuestReply.Event(110); // 4th CS quest "Redeeming Rocks"
                    }
                    else if (redeemingRocks == QuestStatus.Accepted &&
                        q.Player.CharVar("RedeemingDay") != q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(111); // Last CS quest "Redeeming Rocks"
                    }
                    return null; // the script's 140 (Standard text)
                });

            // Rohn_Ehlbalna.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("RedeemingRocksProg") == 1)
                // 2nd CS
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.RohnEhlbalna).Called().OnTrigger(QuestReply.Event(114));

            // Vunkerl_Inlet_[S] qm4.lua onTrigger
            b.AnyState(when: q => q.Player.CharVar("RedeemingRocksProg") == 3)
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm4).Called().OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PieceOfKionite);
                    q.Player.SetCharVar("RedeemingRocksProg", 4);
                    return QuestReply.NoAction();
                });

            // Kocco_Ehllek.lua and Rohn_Ehlbalna.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWatersS, 108, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar("RedeemingRocksProg", 1);
                })
                .OnEventFinish(Zone.WindurstWatersS, 109, q =>
                {
                    q.Player.SetCharVar("RedeemingRocksProg", 3);
                })
                .OnEventFinish(Zone.WindurstWatersS, 110, q =>
                {
                    q.Player.SetCharVar("RedeemingRocksProg", 0);
                    q.Player.SetCharVar("RedeemingDay", q.Player.VanadielUniqueDay);
                })
                .OnEventFinish(Zone.WindurstWatersS, 111, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[7][4] vars
                    if (q.Player.GiveItem(Item.KoccosEarring))
                    {
                        q.Player.SetCharVar("RedeemingDay", 0);
                        q.Player.CompleteQuest(QuestLog.CrystalWar, "REDEEMING_ROCKS");
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 114, q =>
                {
                    q.Player.SetCharVar("RedeemingRocksProg", 2);
                });
        }
    }
}
