// Chocobilious (Log ID 2, Quest ID 27), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Kuoh_Rhel.lua, Matata.lua and Tapoh_Lihzeh.lua (quest rework B3b, Windurst group
// 24; call-only). Kuoh Rhel : !pos 131.437 -6 -102.723 241; Matata : !pos 131 -5 -109 241; Tapoh Lihzeh : !pos 51.011
// -3.749 54.402 241. Tapoh Lihzeh's script calls this quest, then Paying Lip Service. Kuoh Rhel: available, Windurst
// fame level 2: 224 (option 1: begun); completed with needToZone: 232; accepted with ChocobiliousQuest 2: 231;
// accepted: 225; else nothing (his script has no standard). 231's finish: npcUtil.completeQuest's steps
// (Windurst fame 20, 1500 x GIL_RATE gil with GIL_OBTAINED, ChocobiliousQuest cleared, the raw player:completeQuest, so
// the quest's Quest[2][27] vars stay); then (always: no item) needToZone. Matata: completed: 226; else his 223 (kept in
// his script). Tapoh Lihzeh: accepted, var 2: 230; var 1: 228 (0, Papaka Grass); else accepted: 227 (0, Papaka Grass)
// (its finish: var 1). The trade, accepted with var 1: Papaka Grass (npcUtil.tradeHas): 229 (0, Papaka Grass) (its
// finish: var 2). Kept as the Lua has it: 229's finish never confirms the trade (the grass stays with the char);
// the finishes read no status; In a Stew (group 10, a container) answers Kuoh Rhel with a Progress event once this
// quest is completed (fame level 2), before his script.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class Chocobilious : QuestScript
    {
        const string Var = "ChocobiliousQuest";

        public Chocobilious() : base(QuestLog.Windurst, "CHOCOBILIOUS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Kuoh_Rhel.lua onTrigger
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KuohRhel).Called().OnTrigger(q =>
                {
                    QuestStatus chocobilious = q.Quest.Status;

                    if (chocobilious == QuestStatus.Available &&
                        q.Player.FameLevel(FameArea.Windurst) >= 2)
                    {
                        return QuestReply.Event(224); // start quest
                    }
                    else if (chocobilious == QuestStatus.Completed && q.Player.NeedToZone)
                    {
                        return QuestReply.Event(232); // quest complete
                    }
                    else if (chocobilious == QuestStatus.Accepted && q.Player.CharVar(Var) == 2)
                    {
                        return QuestReply.Event(231); // talked to Tapoh
                    }
                    else if (chocobilious == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(225); // post quest accepted
                    }

                    return null;
                })
                // Matata.lua onTrigger (his 223 stays in his script)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Matata).Called().OnTrigger(q =>
                    q.Quest.Status == QuestStatus.Completed ? QuestReply.Event(226) : null)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TapohLihzeh).Called()
                    // Tapoh_Lihzeh.lua onTrade (first)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Player.CharVar(Var) == 1 &&
                            q.Trade.Has(Item.PapakaGrass))
                        {
                            return QuestReply.Event(229, 0, Item.PapakaGrass);
                        }

                        return null;
                    })
                    // Tapoh_Lihzeh.lua onTrigger (first)
                    .OnTrigger(q =>
                    {
                        QuestStatus chocobilious = q.Quest.Status;
                        int chocobiliousCS = q.Player.CharVar(Var);

                        if (chocobilious == QuestStatus.Accepted && chocobiliousCS == 2)
                        {
                            return QuestReply.Event(230); // after trading
                        }
                        else if (chocobilious == QuestStatus.Accepted && chocobiliousCS == 1)
                        {
                            return QuestReply.Event(228, 0, Item.PapakaGrass); // after first talk
                        }
                        else if (chocobilious == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(227, 0, Item.PapakaGrass); // first talk
                        }

                        return null; // Paying Lip Service next
                    });

            b.AnyState()
                // Kuoh_Rhel.lua onEventFinish
                .OnEventFinish(Zone.WindurstWoods, 224, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 231, q =>
                {
                    // npcUtil.completeQuest with fame 20, the fameArea WINDURST, gil 1500 and
                    // the var ChocobiliousQuest, step for step: not quest:complete, whose cleanup would clear the
                    // quest's own Quest[2][27] vars. No item, so it always returns true
                    q.Player.AddFame(FameArea.Windurst, 20);
                    int gil = RateAmount(1500, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWoods.GIL_OBTAINED, gil);
                    q.Player.SetCharVar(Var, 0);
                    q.Player.CompleteQuest(QuestLog.Windurst, "CHOCOBILIOUS");
                    q.Player.SetNeedToZone(true);
                })
                // Tapoh_Lihzeh.lua onEventFinish (227, 229)
                .OnEventFinish(Zone.WindurstWoods, 227, q =>
                {
                    q.Player.SetCharVar(Var, 1);
                })
                .OnEventFinish(Zone.WindurstWoods, 229, q =>
                {
                    q.Player.SetCharVar(Var, 2);
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
