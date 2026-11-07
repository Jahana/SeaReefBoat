// Truth, Justice, and the Onion Way! (Log ID 2, Quest ID 36), a line-for-line port of
// scripts/quests/windurst/SOB1_Truth_Justice_and_the_Onion_Way.lua (quest rework B3b). Kohlo-Lakolo : !pos -26.8 -6 190
// 240. Kohlo-Lakolo's 368 offers it (option 0: begun). Accepted: his 371, the brigade's reminder lines (Gomada-Vulmada
// 373, Papo-Hopo 372, Pichichi 374, Pyo Nzon 375, Shanruru 377, Yafa Yaa 376); a Rarab Tail traded alone plays 378,
// which completes the quest (fame 20, the Justice Badge, Star Onion Brigade Member) and takes the tail. Completed while
// Know One's Onions is available: the brigade's new default lines (replaceDefault). Kept as the Lua file has it: 378
// sets the player local var '[2][40]mustZone', which nothing reads (Know One's Onions reads the char var
// 'Quest[2][40]mustZone').
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TruthJusticeAndTheOnionWay : QuestScript
    {
        public TruthJusticeAndTheOnionWay() : base(QuestLog.Windurst, "TRUTH_JUSTICE_AND_THE_ONION_WAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB1_Truth_Justice_and_the_Onion_Way";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.JusticeBadge,
                Title = Title.StarOnionBrigadeMember,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(QuestReply.ProgressEvent(368)) // Quest starting event.
                .OnEventFinish(Zone.PortWindurst, 368, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo)
                    .OnTrigger(q => QuestReply.Event(371)) // Reminder text.
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.RarabTail))
                        {
                            return QuestReply.ProgressEvent(378, 0, Item.RarabTail, Item.JusticeBadge);
                        }

                        return null;
                    })
                // Reminder text.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(373))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(372))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(374))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(375))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(377))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(376))
                .OnEventFinish(Zone.PortWindurst, 378, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetLocalVar("[2][40]mustZone", 1);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "KNOW_ONES_ONIONS") == QuestStatus.Available)
                // New default texts.
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(QuestReply.Event(381).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(380).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(QuestReply.Event(385).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(QuestReply.Event(382).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(QuestReply.Event(384).ReplaceDefault())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(QuestReply.Event(383).ReplaceDefault());
        }
    }
}
