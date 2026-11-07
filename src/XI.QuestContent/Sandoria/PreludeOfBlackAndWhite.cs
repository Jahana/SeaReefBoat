// Prelude of Black and White (Log ID 0, Quest ID 88, WHM AF2), a line-for-line port of scripts/quests/sandoria/Prelude_of_Black_and_White.lua
// (quest rework B3b, San d'Oria group 4). _6h1 : !pos -37 -3 31 233; Narcheral : !pos 129 -11 126 231. Offered to a WHM of
// AF2_QUEST_LEVEL with Messenger From Beyond completed: the door _6h1's 551 begins it (any option). Accepted: exactly a
// Canteen of Yagudo Holy Water and Moccasins to Narcheral play 691 (completed: Healer's Duckbills, fame +20; the items
// taken only on completion). The _6h1 script (Pieuje's Decision), Borghertz's Healing Hands, RoE records and Vingijard
// read the status (stay).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class PreludeOfBlackAndWhite : QuestScript
    {
        public PreludeOfBlackAndWhite() : base(QuestLog.Sandoria, "PRELUDE_OF_BLACK_AND_WHITE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Prelude_of_Black_and_White";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.HealersDuckbills, Fame = 20, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Sandoria, "MESSENGER_FROM_BEYOND") &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Whm)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h1).OnTrigger(QuestReply.ProgressEvent(551))
                .OnEventFinish(Zone.ChateauDoraguille, 551, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Narcheral).OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly(trade, { xi.item.CANTEEN_OF_YAGUDO_HOLY_WATER, xi.item.MOCCASINS })
                    if (q.Trade.HasExactly((Item.CanteenOfYagudoHolyWater, 1), (Item.Moccasins, 1)))
                    {
                        return QuestReply.ProgressEvent(691);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 691, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
