// Promotion: Corporal (Log ID 6, Quest ID 93), a line-for-line port of scripts/quests/ahtUrhgan/Promotion_Corporal.lua
// (quest rework B3b Aht Urhgan, group 3). Aht Urhgan Whitegate: Naja Salaheem !pos 26 -8 -45.5 (no script, no default).
// The Warhorse Hoofprints of Bhaflau Thickets, Caedarva Mire, Mount Zhayolm and Wajaom Woodlands (no script; default
// WARHORSE_HOOFPRINT; hidden until the Dark Rider's run leaves them: see Promotion: Superior Private).
// Available with AssaultPromotion >= 25 and Promotion: Lance Corporal completed: Naja 5045 (text_table 0), whose finish
// begins it and gives the Quartz Transmitter. Accepted with the transmitter: Naja 5047 once per zone; a Hoofprint takes
// the transmitter, sets Prog to its zone (Wajaom 1, Bhaflau 2, Zhayolm 3, Caedarva 4) and shows WARHORSE_HOOFPRINT + 1
// with the transmitter. Accepted without it: Naja 5046 ([0] = Prog, text_table 0), whose finish completes it (the C
// Wildcat Badge, Corporal) and then sets AssaultPromotion 0 (player:setVar, setCharVar's alias) and takes the LC
// Wildcat Badge.
// Kept as the Lua has it: 5045 begins it on any option; no completion message; a GM's delKeyItem of the transmitter
// reaches 5046 at Prog 0.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionCorporal : QuestScript
    {
        public PromotionCorporal() : base(QuestLog.AhtUrghan, "PROMOTION_CORPORAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Corporal";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.CWildcatBadge,
                Title = Title.Corporal,
            });
            b.Steps(1, 2, 3, 4);

            // Section 1: available, AssaultPromotion >= 25, Lance Corporal completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_LANCE_CORPORAL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5045).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5045, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.QuartzTransmitter);
                });

            // Section 2: accepted, the Quartz Transmitter held
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.QuartzTransmitter))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5047).TextTable(0).OncePerZone())
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.WarhorseHoofprint)
                    .OnTrigger(q => Hoofprint(q, 2, Text.BhaflauThickets.WARHORSE_HOOFPRINT))
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.WarhorseHoofprint)
                    .OnTrigger(q => Hoofprint(q, 4, Text.CaedarvaMire.WARHORSE_HOOFPRINT))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.WarhorseHoofprint)
                    .OnTrigger(q => Hoofprint(q, 3, Text.MountZhayolm.WARHORSE_HOOFPRINT))
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.WarhorseHoofprint)
                    .OnTrigger(q => Hoofprint(q, 1, Text.WajaomWoodlands.WARHORSE_HOOFPRINT));

            // Section 3: accepted, no Quartz Transmitter
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.QuartzTransmitter))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    int hoofprintZone = q.Quest.Step;
                    return QuestReply.ProgressEvent(5046).Option(0, hoofprintZone).TextTable(0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5046, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetCharVar("AssaultPromotion", 0);
                        q.Player.DelKeyItem(KeyItem.LcWildcatBadge);
                    }
                });
        }

        // each zone's Hoofprint: the transmitter taken, Prog the zone's number, then
        // quest:messageSpecial(zones[player:getZoneID()].text.WARHORSE_HOOFPRINT + 1, xi.keyItem.QUARTZ_TRANSMITTER)
        static QuestReply Hoofprint(IQuestContext q, int zoneNumber, ZoneText hoofprintText)
        {
            q.Player.DelKeyItem(KeyItem.QuartzTransmitter);
            q.Quest.SetStep(zoneNumber);
            return QuestReply.MessageSpecial(q.Text.Id(hoofprintText) + 1, KeyItem.QuartzTransmitter);
        }
    }
}
