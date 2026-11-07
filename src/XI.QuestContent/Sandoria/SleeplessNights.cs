// Sleepless Nights (Log ID 0, Quest ID 80), a line-for-line port of scripts/quests/sandoria/Sleepless_Nights.lua (quest
// rework B3b, San d'Oria group 1). Paouala : !pos 158 -6 17 230. Offered at San d'Oria fame level 2: 85 (option 1 begins
// it). Accepted: 83; a Jug of Mary's Milk (tradeHas) plays 84: completed (fame +20, 5000 gil, Sheep's Milk Deliverer),
// the trade confirmed. Completed: 81 (replaceDefault). Paouala's default is 82. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SleeplessNights : QuestScript
    {
        public SleeplessNights() : base(QuestLog.Sandoria, "SLEEPLESS_NIGHTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Sleepless_Nights";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Gil = 5000, Title = Title.SheepsMilkDeliverer });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Paouala).OnTrigger(QuestReply.ProgressEvent(85))
                .OnEventFinish(Zone.SouthernSanDoria, 85, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Paouala)
                    // npcUtil.tradeHas(trade, xi.item.JUG_OF_MARYS_MILK)
                    .OnTrade(q => q.Trade.Has(Item.JugOfMarysMilk) ? QuestReply.ProgressEvent(84) : null)
                    .OnTrigger(QuestReply.Event(83))
                .OnEventFinish(Zone.SouthernSanDoria, 84, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Paouala).OnTrigger(QuestReply.Event(81).ReplaceDefault());
        }
    }
}
