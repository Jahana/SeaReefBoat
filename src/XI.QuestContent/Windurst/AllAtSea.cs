// All at Sea (Log ID 2, Quest ID 23), a line-for-line port of scripts/quests/windurst/All_At_Sea.lua (quest rework
// B3b). Paytah !pos 77.609 -5.000 119.549; Baren-Moren !pos -66.226 -2.009 -148.883. At Windurst fame level 3: Paytah's
// 291; a Ripped Cap traded to him (tradeHas, the cap kept) plays 292, which begins it. Accepted: Odilia's 294, Paytah's
// 293; Baren-Moren (Windurst Waters): the Ripped Cap traded at Prog 0: 547 (0, Dhalmel Hide; taken, Prog 1); 548 at Prog
// 1, 550 at Prog 2; four Dhalmel Hides traded from Prog 1 while no Sailor's Cap is held: 549 (Prog 1: option 0 takes
// them and gives the Sailor's Cap and the Dhalmel Mantle, Prog 2; option 1 takes them and gives the cap; at Prog 2,
// 549 (0, 0, 1): the same options). The Sailor's Cap traded to Paytah: 295, completed (fame 30, the Leather Ring; the
// cap taken; the quest's mustZone). Completed while the mustZone holds: Odilia's 297, Paytah's 296.
// Kept as the Lua file has it: 292's finish does not take the Ripped Cap (Baren-Moren's 547 takes it); 549 confirms the
// hides before giving (a full bag loses them); 549 option 1 at Prog 1 gives the cap without the mantle and keeps Prog 1;
// Baren-Moren's script reads the var Quest[2][23]Prog (Hat in Hand's offer waits for Prog 0).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class AllAtSea : QuestScript
    {
        public AllAtSea() : base(QuestLog.Windurst, "ALL_AT_SEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/All_At_Sea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst, Item = Item.LeatherRing });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 3)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Paytah)
                    .OnTrigger(q => QuestReply.Event(291))
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.RippedCap))
                        {
                            return QuestReply.ProgressEvent(292);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.PortWindurst, 292, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Odilia).OnTrigger(QuestReply.Event(294))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Paytah)
                    .OnTrigger(q => QuestReply.Event(293))
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.SailorsCap))
                        {
                            return QuestReply.ProgressEvent(295);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.PortWindurst, 295, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 1)
                        {
                            return QuestReply.Event(548, 0, Item.DhalmelHide);
                        }
                        else if (progress == 2)
                        {
                            return QuestReply.Event(550, Item.SailorsCap, Item.DhalmelHide);
                        }
                        else
                        {
                            return null;
                        }
                    })
                    .OnTrade(q =>
                    {
                        int progress = q.Quest.Step;

                        if (
                            progress == 0 &&
                            q.Trade.Has(Item.RippedCap))
                        {
                            return QuestReply.ProgressEvent(547, 0, Item.DhalmelHide);
                        }
                        else if (
                            progress >= 1 &&
                            q.Trade.HasQty((Item.DhalmelHide, 4)) &&
                            !q.Player.HasItem(Item.SailorsCap))
                        {
                            if (progress == 1)
                            {
                                return QuestReply.ProgressEvent(549); // First time getting the Sailor's Cap
                            }
                            else if (progress == 2)
                            {
                                return QuestReply.ProgressEvent(549, 0, 0, 1); // Everytime after
                            }
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 547, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.WindurstWaters, 549, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Trade.Confirm();
                        q.Player.GiveItem(Item.SailorsCap);
                        q.Player.GiveItem(Item.DhalmelMantle);
                        q.Quest.SetStep(2);
                    }
                    else if (q.Option == 1)
                    {
                        q.Trade.Confirm();
                        q.Player.GiveItem(Item.SailorsCap);
                    }
                });

            b.State(QuestStatus.Completed, when: q =>
                    q.Quest.MustZone)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Odilia).OnTrigger(QuestReply.Event(297))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Paytah).OnTrigger(QuestReply.Event(296));
        }
    }
}
