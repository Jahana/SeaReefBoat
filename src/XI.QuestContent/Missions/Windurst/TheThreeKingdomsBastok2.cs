// The Three Kingdoms Bastok, part 2 (Windurst 2-3, Log ID 2, Mission ID 9), a line-for-line port of
// scripts/missions/windurst/2_3_4_The_Three_Kingdoms_Bastok2.lua (quest rework C3 N3). Grohm !pos -18 -11 -27 237; Pius
// !pos 99 -21 -12 237; Patt-Pott !pos 23 -17 42 237. One section (this mission current), an empty reward table (no
// Reward call). Metalworks: Grohm at status 9 plays 426 (progress, parameter 1), else 427; Pius at status 8 plays 355
// (progress, parameter 1), below 11 356, else nothing; Patt-Pott at status 11 plays 257 (progress), else 258; the door
// _6ld and Iron Eater play 702; Mih Ketto, Moyoyo and Topuru-Kuperu play 267, 264 and 261 (events). Finishes: 257
// completes the mission and, when that succeeds, begins The Three Kingdoms, takes the Kindred Crest, gives the Kindred
// Report and sets status 11; 355 sets status 9; 426 sets status 10. Waughroon Shrine: the 32001 finish at status 10
// after a win of Rank 2 Mission 2 (the local var battlefieldWin) gives the Kindred Crest, takes the Dark Key and sets
// status 11.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheThreeKingdomsBastok2 : MissionScript
    {
        public TheThreeKingdomsBastok2() : base(XI.Quests.MissionLog.Windurst, "THE_THREE_KINGDOMS_BASTOK2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_3_4_The_Three_Kingdoms_Bastok2";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Grohm).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 9)
                    {
                        return QuestReply.ProgressEvent(426, 1);
                    }
                    else
                    {
                        return QuestReply.Event(427);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Pius).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 8)
                    {
                        return QuestReply.ProgressEvent(355, 1);
                    }
                    else if (missionStatus < 11)
                    {
                        return QuestReply.Event(356);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.PattPott).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(257);
                    }
                    else
                    {
                        return QuestReply.Event(258);
                    }
                })
                // Event 702 is the door line for players on a mission. Both the door and Iron Eater serve it.
                .Npc(Zone.Metalworks, Npc.Metalworks._6ld).OnTrigger(q => QuestReply.Event(702))
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q => QuestReply.Event(702))
                // The staff comment on the errand while the leg runs.
                .Npc(Zone.Metalworks, Npc.Metalworks.MihKetto).OnTrigger(q => QuestReply.Event(267))
                .Npc(Zone.Metalworks, Npc.Metalworks.Moyoyo).OnTrigger(q => QuestReply.Event(264))
                .Npc(Zone.Metalworks, Npc.Metalworks.TopuruKuperu).OnTrigger(q => QuestReply.Event(261))
                .OnEventFinish(Zone.Metalworks, 257, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                        q.Player.SetMissionStatus(AreaId, 11);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 355, q => q.Player.SetMissionStatus(AreaId, 9))
                .OnEventFinish(Zone.Metalworks, 426, q => q.Player.SetMissionStatus(AreaId, 10))
                .OnEventFinish(Zone.WaughroonShrine, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 10 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission2)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.DelKeyItem(KeyItem.DarkKey);
                        q.Player.SetMissionStatus(AreaId, 11);
                    }
                });
        }
    }
}
