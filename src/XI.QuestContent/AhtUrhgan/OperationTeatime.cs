// Operation Teatime (Log ID 6), ported from the quest branches of
// scripts/zones/Aht_Urhgan_Whitegate/npcs/Iruki-Waraki.lua, scripts/zones/Nashmau/npcs/Dnegan.lua and
// scripts/zones/Caedarva_Mire/npcs/qm10.lua (quest rework B3b, Aht Urhgan group 11; call-only: the scripts now call
// this quest). Iruki-Waraki : !pos 101.329 -6.999 -29.042 50; Dnegan : !pos 29.89 -6 55.83 53; qm10 : !pos 473 -31 75
// 79.
// Iruki-Waraki, the last arms of his talk (after The Wayward Automaton's and his 267): a PUP of AF2_QUEST_LEVEL with No
// Strings Attached and The Wayward Automaton completed and this quest available: 778 (its finish:
// OperationTeaTimeProgress 1, begun, any option); accepted at OperationTeaTimeProgress 1: 779; at 2: 781; completed:
// 777. His trade: a Flask of Sleeping Potion and a Cup of Chai (npcUtil.tradeHas) at OperationTeaTimeProgress 1 (any
// status): 780 (its finish: OperationTeaTimeProgress 2, the trade confirmed).
// Dnegan, after The Wayward Automaton's arms: OperationTeatimeProgress 2 and OTT_DayWait not today's
// VanadielUniqueDay(): 290; its option 0: OTT_DayWait today; option 1: OperationTeatimeProgress 3, OTT_DayWait 0. Else
// his 288 (his script).
// qm10: accepted at OperationTeatimeProgress 3: 15 (its finish: npcUtil.completeQuest with item 15602, the Puppetry
// Churidars, the var OperationTeatimeProgress cleared); else NOTHING_OUT_OF_ORDINARY.
// Kept as the Lua has it: Iruki-Waraki writes and reads the char var OperationTeaTimeProgress (capital T), Dnegan and
// qm10 (and the completion's var) OperationTeatimeProgress (small t). A char var's name is matched without case, so
// they are one var (the golden flows show Dnegan reading Iruki-Waraki's 2); both spellings are passed as the Lua has
// them. 778's finish reads no option and no status; the trade's 780 reads no status; Dnegan's 290 reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class OperationTeatime : QuestScript
    {
        // Iruki-Waraki's name of the var
        const string ProgIruki = "OperationTeaTimeProgress";
        // Dnegan's, qm10's and the completion's
        const string Prog = "OperationTeatimeProgress";

        public OperationTeatime() : base(QuestLog.AhtUrghan, "OPERATION_TEATIME") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Iruki-Waraki.lua onTrade
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).Called()
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.FlaskOfSleepingPotion, Item.CupOfChai) &&
                            q.Player.CharVar(ProgIruki) == 1)
                        {
                            return QuestReply.Event(780); // chai, sleeping potion
                        }

                        return null;
                    })
                    // Iruki-Waraki.lua onTrigger: the Operation Teatime arms, last in his chain
                    .OnTrigger(q =>
                    {
                        QuestStatus noStringsAttached = q.Player.QuestStatus(QuestLog.AhtUrghan, "NO_STRINGS_ATTACHED");
                        QuestStatus theWaywardAutomaton =
                            q.Player.QuestStatus(QuestLog.AhtUrghan, "THE_WAYWARD_AUTOMATON");
                        QuestStatus operationTeaTime = q.Quest.Status;
                        int operationTeaTimeProgress = q.Player.CharVar(ProgIruki);

                        if (q.Player.MainJob == Job.Pup &&
                            q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                            noStringsAttached == QuestStatus.Completed &&
                            theWaywardAutomaton == QuestStatus.Completed &&
                            operationTeaTime == QuestStatus.Available)
                        {
                            return QuestReply.Event(778);
                        }
                        else if (operationTeaTime == QuestStatus.Accepted && operationTeaTimeProgress == 1)
                        {
                            return QuestReply.Event(779); // reminds you to get the items
                        }
                        else if (operationTeaTime == QuestStatus.Accepted && operationTeaTimeProgress == 2)
                        {
                            return QuestReply.Event(781);
                        }
                        else if (operationTeaTime == QuestStatus.Completed)
                        {
                            return QuestReply.Event(777);
                        }

                        return null;
                    })
                // Dnegan.lua onTrigger: the Operation Teatime arm, after The Wayward Automaton's
                .Npc(Zone.Nashmau, Npc.Nashmau.Dnegan).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar(Prog) == 2 &&
                        q.Player.CharVar("OTT_DayWait") != q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(290); // the chai cutscene
                    }

                    return null;
                })
                // qm10.lua onTrigger
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.qm10).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Prog) == 3)
                    {
                        return QuestReply.Event(15);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.CaedarvaMire.NOTHING_OUT_OF_ORDINARY);
                    }
                });

            // the scripts' onEventFinish: Iruki-Waraki 778 / 780, Dnegan 290, qm10 15
            b.AnyState()
                .OnEventFinish(Zone.AhtUrhganWhitegate, 778, q =>
                {
                    q.Player.SetCharVar(ProgIruki, 1);
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 780, q =>
                {
                    q.Player.SetCharVar(ProgIruki, 2);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.Nashmau, 290, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetCharVar("OTT_DayWait", q.Player.VanadielUniqueDay);
                    }
                    else if (q.Option == 1)
                    {
                        q.Player.SetCharVar(Prog, 3);
                        q.Player.SetCharVar("OTT_DayWait", 0);
                    }
                })
                .OnEventFinish(Zone.CaedarvaMire, 15, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[6][28] vars
                    if (q.Player.GiveItem(Item.PuppetryChuridars))
                    {
                        q.Player.SetCharVar(Prog, 0);
                        q.Player.CompleteQuest(QuestLog.AhtUrghan, "OPERATION_TEATIME");
                    }
                });
        }
    }
}
