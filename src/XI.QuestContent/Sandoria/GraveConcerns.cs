// Grave Concerns (Log ID 0, Quest ID 11), a line-for-line port of scripts/quests/sandoria/Grave_Concerns.lua (quest rework
// B3b, San d'Oria group 9). Andecia : !pos 167 0 45 230; Tombstone_Upper : !pos 1 0.1 -101 190; Well : !pos -129 -6 92 230
// (every Well of Southern San d'Oria answers: the Lua names them by name). Offered at San d'Oria fame level 1 (always):
// Andecia's 540 (Introduction 1), then 541 (option 0: the Skin of Well Water given, begun; not begun when it cannot be
// given). Accepted: the skin to the Tombstone (KRT) swaps it for the Tomb Guard's Waterskin (OfferingWaterOK 1,
// CHANGE_WATER); the waterskin to Andecia completes it (558, or 624 when the skin was lost on the way; fame +20, 560 gil,
// Royal Grave Keeper; the trade taken). A lost skin: Andecia's 622 (DroppedWaterskin 1), then 623; the Tombstone's 2
// gives the Tomb Guard's Waterskin (finish), and a Well fills it (the Skin of Well Water for it). Completed: Andecia's
// I_THANK_YOU_ADVENTURER. No outside reference.
// Kept as the Lua file has it: the fame level test (>= 1) always holds; the Tombstone's trade wants the skin, not only
// the skin (hasItemQty), and tradeComplete then takes the whole trade; Andecia's onTrigger and onTrade return nil for an
// Introduction / OfferingWaterOK / DroppedWaterskin outside 0 and 1; the Well's trade returns nil after giving.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class GraveConcerns : QuestScript
    {
        public GraveConcerns() : base(QuestLog.Sandoria, "GRAVE_CONCERNS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Grave_Concerns";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Gil = 560, Title = Title.RoyalGraveKeeper });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 1)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Andecia).OnTrigger(q =>
                {
                    int introduction = q.Quest.Var("Introduction");

                    if (introduction == 0)
                    {
                        return QuestReply.ProgressEvent(540);
                    }
                    else if (introduction == 1)
                    {
                        return QuestReply.ProgressEvent(541);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 540, q =>
                {
                    q.Quest.SetVar("Introduction", 1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 541, q =>
                {
                    if (q.Option == 0)
                    {
                        if (q.Player.GiveItem(Item.SkinOfWellWater))
                        {
                            q.Quest.Begin();
                        }
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Andecia)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.TombGuardsWaterskin) &&
                            q.Quest.Var("OfferingWaterOK") == 1)
                        {
                            int droppedWaterskin = q.Quest.Var("DroppedWaterskin");

                            if (droppedWaterskin == 0)
                            {
                                return QuestReply.ProgressEvent(558); // I thank you adventurer (first ending)
                            }
                            else if (droppedWaterskin == 1)
                            {
                                return QuestReply.ProgressEvent(624); // Well you lost the waterskin (second ending)
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int offeringOK = q.Quest.Var("OfferingWaterOK");

                        if (offeringOK == 0)
                        {
                            if (q.Player.HasItem(Item.SkinOfWellWater))
                            {
                                return QuestReply.MessageText(Text.SouthernSanDoria.TO_GET_TO_KING_RANPERRES); // regular reminder
                            }
                            else if (q.Quest.Var("DroppedWaterskin") == 1)
                            {
                                return QuestReply.Event(623); // take skin from grave, fill and return (dropped item reminder)
                            }
                            else
                            {
                                q.Quest.SetVar("DroppedWaterskin", 1);

                                return QuestReply.ProgressEvent(622); // You lost the skin? Go get another one and fill it
                            }
                        }
                        else if (offeringOK == 1)
                        {
                            return QuestReply.MessageText(Text.SouthernSanDoria.TO_GET_TO_KING_RANPERRES); // regular reminder
                        }
                        return null;
                    })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Well)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Var("OfferingWaterOK") == 1)
                        {
                            return QuestReply.MessageText(Text.SouthernSanDoria.DONT_NEED_MORE_WATER); // You don't need more water
                        }
                        else if (!q.Player.HasItem(Item.SkinOfWellWater) &&
                            q.Trade.HasExactly(Item.TombGuardsWaterskin))
                        {
                            if (q.Player.GiveItem(Item.SkinOfWellWater))
                            {
                                q.Trade.Complete();
                            }
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.MessageText(Text.SouthernSanDoria.YOU_FIND_A_WELL)) // You find a well
                .OnEventFinish(Zone.SouthernSanDoria, 558, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 624, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.TombstoneUpper)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Var("OfferingWaterOK") == 0 &&
                            q.Trade.HasItemQty(Item.SkinOfWellWater, 1))
                        {
                            if (q.Player.GiveItem(Item.TombGuardsWaterskin))
                            {
                                q.Trade.Complete();
                                q.Quest.SetVar("OfferingWaterOK", 1);

                                return QuestReply.MessageSpecial(Text.KingRanperresTomb.CHANGE_WATER); // You change the water
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int offeringOK = q.Quest.Var("OfferingWaterOK");

                        if (q.Player.HasItem(Item.SkinOfWellWater) &&
                            offeringOK == 0 &&
                            q.Quest.Var("DroppedWaterskin") == 0)
                        {
                            return QuestReply.ProgressEvent(2); // A waterskin is lying here
                        }
                        else if (!q.Player.HasItem(Item.SkinOfWellWater) &&
                            !q.Player.HasItem(Item.TombGuardsWaterskin) &&
                            offeringOK == 0)
                        {
                            q.Quest.SetVar("DroppedWaterskin", 1);

                            return QuestReply.ProgressEvent(2); // A waterskin is lying here
                        }
                        return null;
                    })
                .OnEventFinish(Zone.KingRanperresTomb, 2, q =>
                {
                    if (q.Quest.Var("DroppedWaterskin") == 1 &&
                        !q.Player.HasItem(Item.SkinOfWellWater))
                    {
                        q.Player.GiveItem(Item.TombGuardsWaterskin);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Andecia)
                    .OnTrigger(QuestReply.MessageText(Text.SouthernSanDoria.I_THANK_YOU_ADVENTURER));
        }
    }
}
