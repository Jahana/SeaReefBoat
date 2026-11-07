// Promotion: Sergeant (Log ID 6, Quest ID 94), a line-for-line port of scripts/quests/ahtUrhgan/Promotion_Sergeant.lua
// (quest rework B3b Aht Urhgan, group 3). Aht Urhgan Whitegate: Naja Salaheem !pos 26 -8 -45.5 (no script, no default),
// trigger area 10 (Balrahn Way: -89 0 -8 .. -71 0 8; the zone's own switch has no case 10). Nashmau: trigger area 1 (in
// front of the boat customs: 0 0 -50 .. 20 0 -35; the zone's handler is empty), Totoroon !pos -13 0 -24 (no script;
// default 246). Caedarva Mire: qm11 !pos 195 2 -616 (no script, no default).
// Available with AssaultPromotion >= 25 and Promotion: Corporal completed: Naja 5055, whose finish at option 0 begins
// it. Prog 0: Naja 5058 once per zone; area 10: 5056, whose finish at option 0 sets Prog 1. Prog 1: Naja 5058 once per
// zone; Nashmau's area 1 while VanadielUniqueDay() > Stage: 303, whose finish at option 2 sets Prog 2, else Stage = the
// next day. Prog 2: Naja 5058 once per zone; Totoroon 252 (an Event); a trade of the Bowl of Sutlac alone: 304, whose
// finish takes it (confirmTrade) and sets Prog 3. Prog 3: Naja 5058 once per zone; qm11 20, whose finish sets Prog 4.
// Prog 4: Naja 5057, whose finish completes it (the S Wildcat Badge, Sergeant) and then sets AssaultPromotion 0, shows
// PROMOTION_SERGEANT and takes the C Wildcat Badge.
// Kept as the Lua has it: 5058's once-per-zone mark is the lookup's local var by NPC, handler and csid
// (interaction_lookup.lua), so one 5058 per zone covers Progs 0 to 3 (promotion_private_first_class#kinds: Prog 1 in
// the zone of Prog 0's 5058 answers the next line); no text_table on these events.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionSergeant : QuestScript
    {
        public PromotionSergeant() : base(QuestLog.AhtUrghan, "PROMOTION_SERGEANT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Sergeant";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.SWildcatBadge,
                Title = Title.Sergeant,
            });
            b.Steps(1, 2, 3, 4);

            // Section 1: available, AssaultPromotion >= 25, Corporal completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_CORPORAL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5055))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5055, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section 2: accepted, Prog 0
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5058).OncePerZone())
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 10, q => QuestReply.ProgressEvent(5056))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5056, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                });

            // Section 3: accepted, Prog 1
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5058).OncePerZone())
                .OnTriggerAreaEnter(Zone.Nashmau, 1, q =>
                    q.Player.VanadielUniqueDay > q.Quest.Var("Stage") ? QuestReply.ProgressEvent(303) : null)
                .OnEventFinish(Zone.Nashmau, 303, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.SetStep(2);
                    }
                    else
                    {
                        q.Quest.SetVar("Stage", q.Player.VanadielUniqueDay + 1);
                    }
                });

            // Section 4: accepted, Prog 2
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5058).OncePerZone())
                .Npc(Zone.Nashmau, Npc.Nashmau.Totoroon)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BowlOfSutlac))
                        {
                            return QuestReply.ProgressEvent(304);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(252))
                .OnEventFinish(Zone.Nashmau, 304, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                });

            // Section 5: accepted, Prog 3
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5058).OncePerZone())
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.qm11).OnTrigger(q => QuestReply.ProgressEvent(20))
                .OnEventFinish(Zone.CaedarvaMire, 20, q => q.Quest.SetStep(4));

            // Section 6: accepted, Prog 4
            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5057))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5057, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetCharVar("AssaultPromotion", 0);
                        q.Text.Special(Text.AhtUrhganWhitegate.PROMOTION_SERGEANT);
                        q.Player.DelKeyItem(KeyItem.CWildcatBadge);
                    }
                });
        }
    }
}
