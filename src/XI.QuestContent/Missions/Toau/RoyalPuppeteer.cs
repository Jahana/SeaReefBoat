// Royal Puppeteer (ToAU 12, Log ID 4, Mission ID 11), a line-for-line port of
// scripts/missions/toau/12_Royal_Puppeteer.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50;
// Pyopyoroon !pos 22.112 0 24.682 53. One section (the mission current). Aht Urhgan Whitegate: Naja plays 3071 (a plain
// event with the mercenary rank). Nashmau: Pyopyoroon plays 277 at status 0, whose finish sets status 1; at status 1 he
// plays 278 (a plain event, once per zone), and trading a Vial of Jody's Acid plays 279, whose finish completes the
// mission (Lost Kingdom current, the Vial of Spectral Scent) and, only then, completes the trade.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class RoyalPuppeteer : MissionScript
    {
        public RoyalPuppeteer() : base(XI.Quests.MissionLog.ToAU, "ROYAL_PUPPETEER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/12_Royal_Puppeteer";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.VialOfSpectralScent,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.LostKingdom,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.Event(3071, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0))
                .Npc(Zone.Nashmau, Npc.Nashmau.Pyopyoroon).OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 && q.Trade.Matches((Item.VialOfJodysAcid, 1)))
                    {
                        return QuestReply.ProgressEvent(279);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(277);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(278).OncePerZone();
                    }

                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 277, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.Nashmau, 279, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
