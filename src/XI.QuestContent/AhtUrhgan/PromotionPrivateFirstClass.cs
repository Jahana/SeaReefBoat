// Promotion: Private First Class (Log ID 6, Quest ID 90), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Private_First_Class.lua (quest rework B3b Aht Urhgan, group 3). Aht Urhgan
// Whitegate: Naja Salaheem !pos 26 -8 -45.5 (no script, no default; the ToAU missions' handlers need their mission
// current). Available with the char var AssaultPromotion >= 25: Naja 5000 (text_table 0), whose finish begins it.
// Accepted: Naja 5001 (an Event, text_table 0); a trade of one Imp Wing: 5002, whose finish completes it (the PFC
// Wildcat Badge, Private First Class) and then sets AssaultPromotion 0, takes the trade (tradeComplete), takes the PSC
// Wildcat Badge and shows PRIVATE_FIRST_CLASS.
// It was XI.Verify.QuestScripts.Live's Lua fixture until group 3 moved the fixture to Embers of His Past (then still
// on Lua); Embers' port (group 6) moved it on to Scattered into Shadow.
// Kept as the Lua has it: 5000 begins it on any option; the trade is taken only when the completion works (a reward of
// a key item and a title, so always).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionPrivateFirstClass : QuestScript
    {
        public PromotionPrivateFirstClass() : base(QuestLog.AhtUrghan, "PROMOTION_PRIVATE_FIRST_CLASS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Private_First_Class";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.PfcWildcatBadge,
                Title = Title.PrivateFirstClass,
            });

            // Section: available, AssaultPromotion >= 25
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("AssaultPromotion") >= 25)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5000).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5000, q => q.Quest.Begin());

            // Section: accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.Event(5001).TextTable(0))
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.ImpWing, 1)))
                        {
                            return QuestReply.ProgressEvent(5002).TextTable(0);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5002, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetCharVar("AssaultPromotion", 0);
                        q.Trade.Complete();
                        q.Player.DelKeyItem(KeyItem.PscWildcatBadge);
                        q.Text.Special(Text.AhtUrhganWhitegate.PRIVATE_FIRST_CLASS);
                    }
                });
        }
    }
}
