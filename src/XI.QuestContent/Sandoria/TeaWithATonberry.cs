// Tea with a Tonberry? (Log ID 0, Quest ID 109), a line-for-line port of scripts/quests/sandoria/Tea_with_a_Tonberry.lua
// (quest rework B3b, San d'Oria group 3). Sobane : !pos -190 -3 97 230; Anguenet : !pos 214.672 -3.013 -527.561 2; Riche :
// !pos 5.945 -3.75 13.612 1; Davoi qm2 : !pos 189.201 1.2553 -383.921 149. Offered once Signed in Blood is completed, at
// San d'Oria fame level 4, without needToZone: Sobane's 738 begins it. Accepted: Sobane's 739, or 740 at Prog 5
// (completed: Willpower Torque, fame +40, Talks with Tonberries). Anguenet (Carpenters' Landing): 27 at Prog 0 (Prog 1),
// 28 (replaceEvent) at Prog 1; the Attohwa Ginseng at Prog 1 plays 29 (the Tonberry Blackboard, Prog 2, the ginseng
// taken). Riche (Phanauet Channel): 5 with the blackboard (Prog 3, the blackboard deleted); at Prog 3
// RICHE_DAVOI_WATERFALL (replaceDefault). Davoi qm2: WHERE_THE_TONBERRY_TOLD_YOU at Prog 3, 126 at Prog 4 (Prog 5),
// NOTHING_TO_DO at Prog 5; the Royal Treasury Gold at Prog 3 pops the Hematic Cyst within a yalm (the gold taken,
// UNDER_ATTACK); its death at Prog 3 sets Prog 4.
// Kept as the Lua has it: 740's finish ignores quest:complete's result (a full inventory leaves it accepted at Prog 5).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TeaWithATonberry : QuestScript
    {
        public TeaWithATonberry() : base(QuestLog.Sandoria, "TEA_WITH_A_TONBERRY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Tea_with_a_Tonberry";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Sandoria, Item = Item.WillpowerTorque, Title = Title.TalksWithTonberries });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "SIGNED_IN_BLOOD") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 4 &&
                    // player:needToZone()
                    !q.Player.NeedToZone)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q => QuestReply.ProgressEvent(738))
                .OnEventFinish(Zone.SouthernSanDoria, 738, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(740);
                    }
                    else
                    {
                        return QuestReply.Event(739);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 740, q => q.Quest.Complete())
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.Anguenet)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 0)
                        {
                            return QuestReply.ProgressEvent(27, 0, Item.PieceOfAttohwaGinseng);
                        }
                        else if (progress == 1)
                        {
                            return QuestReply.ReplaceEvent(28, 0, Item.PieceOfAttohwaGinseng);
                        }
                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            // npcUtil.tradeHas(trade, xi.item.PIECE_OF_ATTOHWA_GINSENG)
                            q.Trade.Has(Item.PieceOfAttohwaGinseng))
                        {
                            return QuestReply.ProgressEvent(29);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.CarpentersLanding, 27, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.CarpentersLanding, 29, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TonberryBlackboard);
                    q.Quest.SetStep(2);
                    q.Trade.Confirm();
                })
                .Npc(Zone.PhanauetChannel, Npc.PhanauetChannel.Riche).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TonberryBlackboard))
                    {
                        return QuestReply.ProgressEvent(5, 1, 627, Item.IngotOfRoyalTreasuryGold, 63, 3, 30, 30, 0);
                    }
                    else if (q.Quest.Step == 3)
                    {
                        return QuestReply.MessageName(Text.PhanauetChannel.RICHE_DAVOI_WATERFALL, 0, 0, Item.IngotOfRoyalTreasuryGold).ReplaceDefault();
                    }
                    return null;
                })
                .OnEventFinish(Zone.PhanauetChannel, 5, q =>
                {
                    q.Quest.SetStep(3);
                    // no message: the cutscene ends with the NPC taking it
                    q.Player.DelKeyItem(KeyItem.TonberryBlackboard);
                })
                .Npc(Zone.Davoi, Npc.Davoi.qm2)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 3)
                        {
                            return QuestReply.MessageSpecial(Text.Davoi.WHERE_THE_TONBERRY_TOLD_YOU, 0, Item.IngotOfRoyalTreasuryGold);
                        }
                        else if (progress == 4)
                        {
                            return QuestReply.Event(126, 149, Item.IngotOfRoyalTreasuryGold);
                        }
                        else if (progress == 5)
                        {
                            return QuestReply.MessageSpecial(Text.Davoi.NOTHING_TO_DO);
                        }
                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 3 &&
                            // npcUtil.tradeHas(trade, xi.item.INGOT_OF_ROYAL_TREASURY_GOLD)
                            q.Trade.Has(Item.IngotOfRoyalTreasuryGold) &&
                            // npcUtil.popFromQM(player, npc, davoiID.mob.HEMATIC_CYST, { radius = 1, hide = 0 }) (claim defaults to true)
                            q.Player.PopFromQm(Mob.Davoi.HEMATIC_CYST, claim: true, hide: 0, radius: 1))
                        {
                            q.Trade.Confirm();
                            return QuestReply.MessageSpecial(Text.Davoi.UNDER_ATTACK);
                        }
                        return null;
                    })
                .Npc(Zone.Davoi, Npc.Davoi.HematicCyst).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.Davoi, 126, q => q.Quest.SetStep(5));
        }
    }
}
