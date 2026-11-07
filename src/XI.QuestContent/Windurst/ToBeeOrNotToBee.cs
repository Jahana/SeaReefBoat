// To Bee or Not to Bee? (Log ID 2, Quest ID 35), ported from the quest branches of
// scripts/zones/Windurst_Walls/npcs/Zayhi-Bauhi.lua, Raamimi.lua, Kalupa-Tawalupa.lua and Rutango-Botango.lua (quest
// rework B3b, Windurst group 23; call-only). Zayhi-Bauhi : X:-91 Y:-9 Z:109; Raamimi : X:-81 Y:-9 Z:103;
// Kalupa-Tawalupa : X:-90 Y:-9 Z:109; Rutango-Botango : X:-92 Y:-9 Z:107 (Windurst Walls).
// Zayhi-Bauhi: Windurst fame level 2, The Postman Always KO's Twice completed and this quest available, or accepted
// with ToBeeOrNot_var 10: 64 (its finish: var 10). Accepted: var 1 / 2 / 3 / 4: 69 / 70 / 73 / 74; another var:
// nothing. Completed with needToZone: 78. Else his 299 (kept in his script). The trade, accepted, one Pot of Honey
// and nothing else: var 10 / 1 / 2 / 3 / 4: 69 / 70 / 73 / 74 / 75. The finishes of 69 / 70 / 73 / 74:
// player:tradeComplete(), var 1 / 2 / 3 / 4; of 75: the trade taken, var 5, Windurst fame 20, completed, needToZone.
// Raamimi: var 10 and available: 67 (its finish: no free slot: ITEM_CANNOT_BE_OBTAINED (honey); else begun, a Pot of
// Honey, ITEM_OBTAINED); accepted: 68; completed and var 5: 80 (its finish: no free slot: ITEM_CANNOT_BE_OBTAINED
// (4156); else var 0, 3 Bottles of Mulsum, ITEMS_OBTAINED, needToZone); completed, var 0 and needToZone: 79; else
// his 296.
// Kalupa-Tawalupa / Rutango-Botango: var 10: 66 / 65; accepted and var > 0: 72 / 71; completed with needToZone: 77 /
// 76; else their 298 / 297.
// Kept as the Lua has it: 64 plays to an available char before Raamimi's start and sets the var with no quest; a
// trigger's 69-74 finish sets the var it already has (and tradeComplete with no trade); the var-10 arms of
// Kalupa-Tawalupa and Rutango-Botango read no status; the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ToBeeOrNotToBee : QuestScript
    {
        const string Var = "ToBeeOrNot_var";

        public ToBeeOrNotToBee() : base(QuestLog.Windurst, "TO_BEE_OR_NOT_TO_BEE") { }

        static void Honey(IQuestContext q, int var)
        {
            q.Trade.Complete();
            q.Player.SetCharVar(Var, var);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZayhiBauhi).Called()
                    // Zayhi-Bauhi.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            if (q.Trade.HasItemQty(Item.PotOfHoney, 1) && q.Trade.ItemCount == 1)
                            {
                                int toBeeOrNotStatus = q.Player.CharVar(Var);

                                if (toBeeOrNotStatus == 10)
                                {
                                    return QuestReply.Event(69); // after honey 1
                                }
                                else if (toBeeOrNotStatus == 1)
                                {
                                    return QuestReply.Event(70); // after honey 2
                                }
                                else if (toBeeOrNotStatus == 2)
                                {
                                    return QuestReply.Event(73); // after honey 3
                                }
                                else if (toBeeOrNotStatus == 3)
                                {
                                    return QuestReply.Event(74); // after honey 4
                                }
                                else if (toBeeOrNotStatus == 4)
                                {
                                    return QuestReply.Event(75); // after honey 5: quest finish
                                }
                            }
                        }

                        return null;
                    })
                    // Zayhi-Bauhi.lua onTrigger (his 299 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus toBee = q.Quest.Status;
                        QuestStatus postmanKOsTwice =
                            q.Player.QuestStatus(QuestLog.Windurst, "THE_POSTMAN_ALWAYS_KOS_TWICE");
                        int toBeeOrNotStatus = q.Player.CharVar(Var);

                        if ((q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                            postmanKOsTwice == QuestStatus.Completed &&
                            toBee == QuestStatus.Available) ||
                            (toBee == QuestStatus.Accepted && toBeeOrNotStatus == 10))
                        {
                            return QuestReply.Event(64); // just before the quest start
                        }
                        else if (toBee == QuestStatus.Accepted)
                        {
                            if (toBeeOrNotStatus == 1)
                            {
                                return QuestReply.Event(69);
                            }
                            else if (toBeeOrNotStatus == 2)
                            {
                                return QuestReply.Event(70);
                            }
                            else if (toBeeOrNotStatus == 3)
                            {
                                return QuestReply.Event(73);
                            }
                            else if (toBeeOrNotStatus == 4)
                            {
                                return QuestReply.Event(74);
                            }

                            // the inner if has no else: nothing plays, his 299 is not reached
                            return QuestReply.NoAction();
                        }
                        else if (toBee == QuestStatus.Completed && q.Player.NeedToZone)
                        {
                            return QuestReply.Event(78); // after the quest, before a zone change
                        }

                        return null; // his 299
                    })
                // Raamimi.lua onTrigger (his 296 stays in his script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Raamimi).Called().OnTrigger(q =>
                {
                    QuestStatus toBee = q.Quest.Status;
                    int toBeeOrNotStatus = q.Player.CharVar(Var);

                    if (toBeeOrNotStatus == 10 && toBee == QuestStatus.Available)
                    {
                        return QuestReply.Event(67); // the start: he gives honey
                    }
                    else if (toBee == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(68);
                    }
                    else if (toBee == QuestStatus.Completed && toBeeOrNotStatus == 5)
                    {
                        return QuestReply.Event(80); // the Mulsum
                    }
                    else if (toBee == QuestStatus.Completed &&
                        toBeeOrNotStatus == 0 &&
                        q.Player.NeedToZone)
                    {
                        return QuestReply.Event(79);
                    }

                    return null; // his 296
                })
                // Kalupa-Tawalupa.lua onTrigger (his 298 stays in his script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KalupaTawalupa).Called().OnTrigger(q =>
                {
                    QuestStatus toBee = q.Quest.Status;
                    int toBeeOrNotStatus = q.Player.CharVar(Var);

                    if (toBeeOrNotStatus == 10)
                    {
                        return QuestReply.Event(66);
                    }
                    else if (toBee == QuestStatus.Accepted && toBeeOrNotStatus > 0)
                    {
                        return QuestReply.Event(72);
                    }
                    else if (toBee == QuestStatus.Completed && q.Player.NeedToZone)
                    {
                        return QuestReply.Event(77);
                    }

                    return null;
                })
                // Rutango-Botango.lua onTrigger (his 297 stays in his script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.RutangoBotango).Called().OnTrigger(q =>
                {
                    QuestStatus toBee = q.Quest.Status;
                    int toBeeOrNotStatus = q.Player.CharVar(Var);

                    if (toBeeOrNotStatus == 10)
                    {
                        return QuestReply.Event(65);
                    }
                    else if (toBee == QuestStatus.Accepted && toBeeOrNotStatus > 0)
                    {
                        return QuestReply.Event(71);
                    }
                    else if (toBee == QuestStatus.Completed && q.Player.NeedToZone)
                    {
                        return QuestReply.Event(76);
                    }

                    return null;
                });

            b.AnyState()
                // Zayhi-Bauhi.lua onEventFinish
                .OnEventFinish(Zone.WindurstWalls, 64, q =>
                {
                    q.Player.SetCharVar(Var, 10);
                })
                .OnEventFinish(Zone.WindurstWalls, 69, q => Honey(q, 1))
                .OnEventFinish(Zone.WindurstWalls, 70, q => Honey(q, 2))
                .OnEventFinish(Zone.WindurstWalls, 73, q => Honey(q, 3))
                .OnEventFinish(Zone.WindurstWalls, 74, q => Honey(q, 4))
                .OnEventFinish(Zone.WindurstWalls, 75, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar(Var, 5);
                    q.Player.AddFame(FameArea.Windurst, 20);
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][35] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "TO_BEE_OR_NOT_TO_BEE");
                    q.Player.SetNeedToZone(true);
                })
                // Raamimi.lua onEventFinish
                .OnEventFinish(Zone.WindurstWalls, 67, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWalls.ITEM_CANNOT_BE_OBTAINED, Item.PotOfHoney);
                    }
                    else
                    {
                        q.Quest.Begin();
                        q.Player.AddItem(Item.PotOfHoney);
                        q.Text.Special(Text.WindurstWalls.ITEM_OBTAINED, Item.PotOfHoney);
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 80, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWalls.ITEM_CANNOT_BE_OBTAINED, Item.BottleOfMulsum);
                    }
                    else
                    {
                        q.Player.SetCharVar(Var, 0);
                        q.Player.AddItem(Item.BottleOfMulsum, 3);
                        q.Text.Special(Text.WindurstWalls.ITEMS_OBTAINED, Item.BottleOfMulsum, 3);
                        q.Player.SetNeedToZone(true);
                    }
                });
        }
    }
}
