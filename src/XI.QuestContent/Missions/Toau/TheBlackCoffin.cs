// The Black Coffin (ToAU 15, Log ID 4, Mission ID 14), a line-for-line port of
// scripts/missions/toau/15_The_Black_Coffin.lua (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current). Aht Urhgan Whitegate: Naja plays the plain event 3073 (the mercenary rank, and 1).
// Arrapago Reef: trigger area 1 with the Ephramadian Gold Coin and status 0 starts events 8 and 34 itself (34 hidden,
// its seventh parameter 1) and returns the hidden progress event 35, whose finish sets status 1; without the coin at
// status 1 it plays 12 once per zone. The zone-in from The Ashu Talif at status 2 sets the char down at -444.059
// -4.124 -413.934 (rotation 126) and plays 9, whose finish sets status 3 and sends the char to Nashmau at 0, 0, 0.
// Nashmau: the zone-in from Arrapago Reef at status 3 with the position exactly 0, 0, 0 sets the char at 0.016 0
// -23.753 (rotation 63) and plays 281, whose finish completes the mission (Ghosts of the Past current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class TheBlackCoffin : MissionScript
    {
        public TheBlackCoffin() : base(XI.Quests.MissionLog.ToAU, "THE_BLACK_COFFIN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/15_The_Black_Coffin";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.GhostsOfThePast,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3073, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.ArrapagoReef, 1, q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.EphramadianGoldCoin) && q.Player.MissionStatus(AreaId) == 0)
                    {
                        q.Player.StartEvent(8);
                        q.Player.StartEventHiddenParam(34, 7, 1);
                        return QuestReply.ProgressEvent(35).Hidden();
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.EphramadianGoldCoin) && q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.Event(12).OncePerZone();
                    }

                    return null;
                })
                .OnZoneIn(Zone.ArrapagoReef, q =>
                {
                    if (q.Player.PreviousZone == Zone.TheAshuTalif && q.Player.MissionStatus(AreaId) == 2)
                    {
                        q.Player.SetPos(-444.059, -4.124, -413.934, 126);
                        return QuestReply.Event(9);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 9, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.SetPos(0, 0, 0, 0, Zone.Nashmau);
                })
                .OnEventFinish(Zone.ArrapagoReef, 35, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnZoneIn(Zone.Nashmau, q =>
                {
                    if (q.Player.PreviousZone == Zone.ArrapagoReef &&
                        q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.XPos == 0 &&
                        q.Player.YPos == 0 &&
                        q.Player.ZPos == 0)
                    {
                        q.Player.SetPos(0.016, 0, -23.753, 63);
                        return QuestReply.Event(281);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 281, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
