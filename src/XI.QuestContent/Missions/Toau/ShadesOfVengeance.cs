// Shades of Vengeance (ToAU 31, Log ID 4, Mission ID 30), a line-for-line port of
// scripts/missions/toau/31_Shades_of_Vengeance.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3119 (the mercenary rank and 1). Caedarva Mire: Nahshib, without the
// Periqia Assault Area Entry Permit and with VanadielUniqueDay at or past the Timer var, plays the progress event 22,
// whose finish gives the permit (the permit and the Timer are the two parts of its condition); the zone-in at status 1
// plays the plain event 21, whose finish completes the mission (In the Blood current, the Nashmeira's Mercenary
// title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ShadesOfVengeance : MissionScript
    {
        public ShadesOfVengeance() : base(XI.Quests.MissionLog.ToAU, "SHADES_OF_VENGEANCE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/31_Shades_of_Vengeance";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.NashmeirasMercenary,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.InTheBlood,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3119, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Nahshib).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PeriqiaAssaultAreaEntryPermit) &&
                        q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(22);
                    }

                    return null;
                })
                .OnZoneIn(Zone.CaedarvaMire, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.Event(21);
                    }

                    return null;
                })
                .OnEventFinish(Zone.CaedarvaMire, 21, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.CaedarvaMire, 22, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PeriqiaAssaultAreaEntryPermit);
                });
        }
    }
}
