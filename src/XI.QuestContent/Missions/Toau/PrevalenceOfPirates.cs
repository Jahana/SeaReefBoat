// Prevalence of Pirates (ToAU 30, Log ID 4, Mission ID 29), a line-for-line port of
// scripts/missions/toau/30_Prevalence_of_Pirates.lua (quest rework C3 T3). One section (the mission current). Aht
// Urhgan Whitegate: Naja Salaheem plays the plain event 3118 (the mercenary rank and 1). Arrapago Reef: the zone-in at
// status 0 (from any survival guide) plays the plain event 13, whose finish sets status 1; trigger area 1 at status 1
// plays the progress event 14 (a 4 at the second operand), whose finish completes the mission (Shades of Vengeance
// current, the Periqia Assault Area Entry Permit key item).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PrevalenceOfPirates : MissionScript
    {
        public PrevalenceOfPirates() : base(XI.Quests.MissionLog.ToAU, "PREVALENCE_OF_PIRATES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/30_Prevalence_of_Pirates";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PeriqiaAssaultAreaEntryPermit,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ShadesOfVengeance,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3118, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnZoneIn(Zone.ArrapagoReef, q =>
                {
                    // Works from any survival guide.
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(13);
                    }

                    return null;
                })
                .OnTriggerAreaEnter(Zone.ArrapagoReef, 1, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(14, 0, 4, 0, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 13, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.ArrapagoReef, 14, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
