// Twinstone Bonding (Log ID 2, Quest ID 62), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Gioh_Ajihri.lua and Wani_Casdohry.lua (quest rework B3b, Windurst group 28;
// call-only). Gioh Ajihri and Wani Casdohry : Windurst Woods.
// Gioh Ajihri: completed: needToZone 491 (0, Twinstone Earring), else 488 (the same); accepted 488; available with
// Windurst fame level 2 or more: 487 (the same) (its finish, any option: begun, GiohAijhriSpokenTo 1); else his 424
// (kept in his script). 488's finish: the var 1. The trade, the var 1 and no needToZone (any status): a Twinstone
// Earring (npcUtil.tradeHas): 490; its finish: the trade confirmed, needToZone, the var 0; accepted:
// npcUtil.completeQuest's steps (a Wrapped Bow, stopping when it cannot be given; Windurst fame 16; Bond Fixer; the
// raw player:completeQuest, so the quest's Quest[2][62] vars stay); else Windurst fame 16 and 900 gil
// (npcUtil.giveCurrency). Wani Casdohry: completed 492 (0, Twinstone Earring); accepted 489 (the same); else Mihgo's
// Amigo's 86 or his 425 (kept in his script). Lost Chick answers Gioh's 424 updates (its own container).
// Kept as the Lua has it: 490's finish confirms the trade before the completion (the earring's slot is freed first, so
// the bow fits a full inventory); the trade reads the var and needToZone, not the status (a GM's var 1 on an available
// char takes the repeat's branch: fame and gil, no quest); 487 begins on any option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TwinstoneBonding : QuestScript
    {
        const string SpokenTo = "GiohAijhriSpokenTo";

        public TwinstoneBonding() : base(QuestLog.Windurst, "TWINSTONE_BONDING") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.GiohAjihri).Called()
                    // Gioh_Ajihri.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Player.CharVar(SpokenTo) == 1 &&
                            !q.Player.NeedToZone &&
                            q.Trade.Has(Item.TwinstoneEarring))
                        {
                            return QuestReply.Event(490);
                        }

                        return null;
                    })
                    // Gioh_Ajihri.lua onTrigger (his 424 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus twinstoneBonding = q.Quest.Status;

                        if (twinstoneBonding == QuestStatus.Completed)
                        {
                            if (q.Player.NeedToZone)
                            {
                                return QuestReply.Event(491, 0, Item.TwinstoneEarring);
                            }
                            else
                            {
                                return QuestReply.Event(488, 0, Item.TwinstoneEarring);
                            }
                        }
                        else if (twinstoneBonding == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(488, 0, Item.TwinstoneEarring);
                        }
                        else if (twinstoneBonding == QuestStatus.Available &&
                            q.Player.FameLevel(FameArea.Windurst) >= 2)
                        {
                            return QuestReply.Event(487, 0, Item.TwinstoneEarring);
                        }

                        return null;
                    })
                // Wani_Casdohry.lua onTrigger (Mihgo's Amigo's 86 and his 425 stay in his script)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.WaniCasdohry).Called().OnTrigger(q =>
                {
                    QuestStatus twinstoneBonding = q.Quest.Status;

                    if (twinstoneBonding == QuestStatus.Completed)
                    {
                        return QuestReply.Event(492, 0, Item.TwinstoneEarring);
                    }
                    else if (twinstoneBonding == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(489, 0, Item.TwinstoneEarring);
                    }

                    return null;
                });

            // Gioh_Ajihri.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 487, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(SpokenTo, 1);
                })
                .OnEventFinish(Zone.WindurstWoods, 490, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetNeedToZone(true);
                    q.Player.SetCharVar(SpokenTo, 0);

                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // npcUtil.completeQuest with the Wrapped Bow (17154), fame 16, the fameArea WINDURST and the
                        // title BOND_FIXER, step for step: not quest:complete, whose cleanup would clear the quest's
                        // own Quest[2][62] vars. Nothing more is done when the bow cannot be given
                        if (q.Player.GiveItem(Item.WrappedBow))
                        {
                            q.Player.AddFame(FameArea.Windurst, 16);
                            q.Player.AddTitle(Title.BondFixer);
                            q.Player.CompleteQuest(QuestLog.Windurst, "TWINSTONE_BONDING");
                        }
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.GiveCurrency("gil", 900);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 488, q =>
                {
                    q.Player.SetCharVar(SpokenTo, 1);
                });
        }
    }
}
