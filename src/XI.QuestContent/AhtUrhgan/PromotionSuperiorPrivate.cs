// Promotion: Superior Private (Log ID 6, Quest ID 91), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Superior_Private.lua (quest rework B3b Aht Urhgan, group 3). Aht Urhgan Whitegate:
// Naja Salaheem !pos 26 -8 -45.5 (no script, no default). Bhaflau Thickets, Caedarva Mire, Mount Zhayolm, Wajaom
// Woodlands: the Warhorse Hoofprints (three a zone; no script; default WARHORSE_HOOFPRINT), hidden (npcs.yaml gives no
// status: DISAPPEAR) until the Dark Rider's run leaves them (globals/dark_rider.lua: the rests set DISAPPEAR and move
// them; its despawn sets NORMAL).
// Available with AssaultPromotion >= 25 and Promotion: Private First Class completed: Naja 5020 (text_table 0), whose
// finish begins it and sets its mustZone. Accepted without the Dark Rider Hoofprint: Naja 5021 once per zone (an Event;
// with the mustZone set, [0] = 1, [1] = 1); a Hoofprint in any of the four zones: the key item (npcUtil.giveKeyItem).
// Accepted with it: Naja 5022, whose finish completes it (the SP Wildcat Badge, Superior Private) and then sets
// AssaultPromotion 0, takes the PFC Wildcat Badge and the hoofprint and shows SUPERIOR_PRIVATE.
// Kept as the Lua has it: 5020 begins it on any option; the mustZone only changes 5021's options.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionSuperiorPrivate : QuestScript
    {
        public PromotionSuperiorPrivate() : base(QuestLog.AhtUrghan, "PROMOTION_SUPERIOR_PRIVATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Superior_Private";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.SpWildcatBadge,
                Title = Title.SuperiorPrivate,
            });

            // Section 1: available, AssaultPromotion >= 25, Private First Class completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_PRIVATE_FIRST_CLASS"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5020).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5020, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetMustZone();
                });

            // Section 2: accepted, no Dark Rider Hoofprint
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.DarkRiderHoofprint))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(5021).Option(0, 1).Option(1, 1).TextTable(0).OncePerZone();
                    }
                    else
                    {
                        return QuestReply.Event(5021).TextTable(0).OncePerZone();
                    }
                })
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.WarhorseHoofprint)
                    .OnTrigger(q => QuestReply.KeyItem(KeyItem.DarkRiderHoofprint))
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.WarhorseHoofprint)
                    .OnTrigger(q => QuestReply.KeyItem(KeyItem.DarkRiderHoofprint))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.WarhorseHoofprint)
                    .OnTrigger(q => QuestReply.KeyItem(KeyItem.DarkRiderHoofprint))
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.WarhorseHoofprint)
                    .OnTrigger(q => QuestReply.KeyItem(KeyItem.DarkRiderHoofprint));

            // Section 3: accepted, the Dark Rider Hoofprint held
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.DarkRiderHoofprint))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5022).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5022, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetCharVar("AssaultPromotion", 0);
                        q.Player.DelKeyItem(KeyItem.PfcWildcatBadge);
                        q.Player.DelKeyItem(KeyItem.DarkRiderHoofprint);
                        q.Text.Special(Text.AhtUrhganWhitegate.SUPERIOR_PRIVATE);
                    }
                });
        }
    }
}
