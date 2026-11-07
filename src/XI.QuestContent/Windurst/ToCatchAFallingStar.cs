// To Catch a Falling Star (Log ID 2, Quest ID 53), ported from the quest branches of
// scripts/zones/Port_Windurst/npcs/Sigismund.lua, Tohopka.lua and scripts/zones/West_Sarutabaruta/npcs/Twinkle_Tree.lua
// (quest rework B3b, Windurst group 27; call-only). Sigismund : !pos -110 -10 82 240; Tohopka : !pos -105.723 -10
// 83.813 240; Twinkle Tree : !pos 156.003 -40.753 333.742 115.
// Sigismund: available 196 (0, Starfall Tear) (its finish, any option: begun); accepted 197 (the same); completed with
// QuestCatchAFallingStar_prog > 0: 200, the var 0; else his 357 (kept in his script). The trade, accepted: one Starfall
// Tear and nothing else, no gil: 199 (its finish: the trade taken, completed, Windurst fame 20, the Fish Scale Shield
// by player:addItem and ITEM_OBTAINED, prog 2). Tohopka, accepted: 198 (0, Starfall Tear, Handful of Pugil Scales);
// else her 358. The Twinkle Tree's trade, accepted and VanadielHour() <= 3: Pugil Scales (npcUtil.tradeHas) at prog 0:
// FROST_DEPOSIT_TWINKLES and MELT_BARE_HANDS, then npcUtil.giveItem (the Starfall Tear): given: the trade confirmed,
// prog 1; and no event (NoAction; its script ignores the reply). The tree's trigger reads only the hour and prog (no
// quest): it stays in its script.
// Kept as the Lua has it: 196 begins on any option; 200's arm writes the var before the reply (the Lua after its
// startEvent: no packet goes between them); 199's finish reads no status and gives the shield with no space check (the
// tear's slot is freed first, so it always fits); the tree's two messages play even when npcUtil.giveItem refuses the
// tear (held already, rare, or no room), and nothing is taken then.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ToCatchAFallingStar : QuestScript
    {
        const string Prog = "QuestCatchAFallingStar_prog";

        public ToCatchAFallingStar() : base(QuestLog.Windurst, "TO_CATCH_A_FALLING_STAR") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Sigismund).Called()
                    // Sigismund.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Trade.HasItemQty(Item.StarfallTear, 1) &&
                            q.Trade.ItemCount == 1 &&
                            q.Trade.Gil == 0)
                        {
                            return QuestReply.Event(199); // quest finish
                        }

                        return null;
                    })
                    // Sigismund.lua onTrigger (his 357 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus starstatus = q.Quest.Status;

                        if (starstatus == QuestStatus.Available)
                        {
                            return QuestReply.Event(196, 0, Item.StarfallTear); // quest start
                        }
                        else if (starstatus == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(197, 0, Item.StarfallTear); // quest reminder
                        }
                        else if (starstatus == QuestStatus.Completed && q.Player.CharVar(Prog) > 0)
                        {
                            q.Player.SetCharVar(Prog, 0);
                            return QuestReply.Event(200); // after the quest
                        }

                        return null;
                    })
                // Tohopka.lua onTrigger (her 358 stays in her script)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tohopka).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(198, 0, Item.StarfallTear, Item.HandfulOfPugilScales);
                    }

                    return null;
                })
                // Twinkle_Tree.lua onTrade
                .Npc(Zone.WestSarutabaruta, Npc.WestSarutabaruta.TwinkleTree).Called().OnTrade(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.VanadielHour <= 3)
                    {
                        if (q.Trade.Has(Item.HandfulOfPugilScales) && q.Player.CharVar(Prog) == 0)
                        {
                            q.Text.Special(Text.WestSarutabaruta.FROST_DEPOSIT_TWINKLES);
                            q.Text.Special(Text.WestSarutabaruta.MELT_BARE_HANDS);

                            if (q.Player.GiveItem(Item.StarfallTear))
                            {
                                q.Trade.Confirm();
                                q.Player.SetCharVar(Prog, 1);
                            }

                            return QuestReply.NoAction(); // no event: the messages are the answer
                        }
                    }

                    return null;
                });

            // Sigismund.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.PortWindurst, 196, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.PortWindurst, 199, q =>
                {
                    q.Trade.Complete();
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][53] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "TO_CATCH_A_FALLING_STAR");
                    q.Player.AddFame(FameArea.Windurst, 20);
                    q.Player.AddItem(Item.FishScaleShield);
                    q.Text.Special(Text.PortWindurst.ITEM_OBTAINED, Item.FishScaleShield);
                    q.Player.SetCharVar(Prog, 2);
                });
        }
    }
}
