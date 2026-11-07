// A Sentry's Peril (Log ID 0, Quest ID 0), a line-for-line port of scripts/quests/sandoria/A_Sentrys_Peril.lua (quest
// rework B3b, San d'Oria group 10). Glenne : !pos -122 -2 15 230; Aaveleon : !pos -431 -45 343 100. Available: Glenne's
// 510 (option 0 and the Dose of Ointment given: begun). Accepted, TradedAaveleon 0: Glenne's 520 with the ointment, 644
// without it (finish: the ointment again); any trade to Glenne 514. The ointment to Aaveleon (West Ronfaure): 118 with no
// free slot, else 100 (finish: the Ointment Case, the ointment taken, TradedAaveleon 1); anything else to him 106.
// TradedAaveleon 1: Aaveleon's AAVELEON_HEALED with the case, 126 without it (option 1: the case again); Glenne's 520;
// the case to Glenne 513 (completed: Bronze Subligar, fame +20, Ronfaurian Rescuer; the case taken), anything else
// 514. Completed: Glenne's 521, Aaveleon's AAVELEON_HEALED. No outside reference.
// Kept as the Lua file has it: 644's and 126's gives ignore their result; the free-slot test reads exactly 0.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ASentrysPeril : QuestScript
    {
        public ASentrysPeril() : base(QuestLog.Sandoria, "A_SENTRYS_PERIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Sentrys_Peril";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Title = Title.RonfaurianRescuer, Item = Item.BronzeSubligar });

            // Talk to Glenne; she's worried about her husband, Aaveleon, a guard out on patrol, and gives you some healing
            // ointment to take to him.
            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Glenne).OnTrigger(QuestReply.ProgressEvent(510))
                .OnEventFinish(Zone.SouthernSanDoria, 510, q =>
                {
                    if (q.Option == 0 && q.Player.GiveItem(Item.DoseOfOintment))
                    {
                        q.Quest.Begin();
                    }
                });

            // Find him on the road to Ghelsba Outpost in West Ronfaure (G-6). Trade Ointment to him and he'll give you the
            // Ointment Case, so you can give it back to his wife.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("TradedAaveleon") == 0)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Glenne)
                    .OnTrade(q => QuestReply.Event(514)) // "I cannot accept this. Take it back."
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.DoseOfOintment))
                        {
                            return QuestReply.Event(520); // reminder to deliver ointment
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(644); // reacquire ointment
                        }
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 644, q =>
                {
                    q.Player.GiveItem(Item.DoseOfOintment);
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Aaveleon).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.DoseOfOintment))
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            return QuestReply.Event(118); // "Ah...but it seems you're already carrying too much."
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(100);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(106); // "What's this? I can't accept gifts from strangers."
                    }
                })
                .OnEventFinish(Zone.WestRonfaure, 100, q =>
                {
                    if (q.Player.GiveItem(Item.OintmentCase))
                    {
                        q.Trade.Confirm();
                        q.Quest.SetVar("TradedAaveleon", 1);
                    }
                });

            // Trade the Ointment Case to Glenne to complete the quest.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("TradedAaveleon") == 1)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Aaveleon).OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.OintmentCase))
                    {
                        return QuestReply.MessageText(Text.WestRonfaure.AAVELEON_HEALED); // "My wounds are healed, thanks to you!"
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(126, Item.OintmentCase); // reacquire ointment case
                    }
                })
                .OnEventFinish(Zone.WestRonfaure, 126, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveItem(Item.OintmentCase);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Glenne)
                    .OnTrigger(QuestReply.Event(520)) // reminder to deliver ointment
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.OintmentCase))
                        {
                            return QuestReply.ProgressEvent(513);
                        }
                        else
                        {
                            return QuestReply.Event(514); // "I cannot accept this. Take it back."
                        }
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 513, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            // Section: After quest completion
            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Glenne).OnTrigger(QuestReply.Event(521))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Aaveleon).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.AAVELEON_HEALED));
        }
    }
}
