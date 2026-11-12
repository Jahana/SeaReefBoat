// The Three Kingdoms Bastok, part 1 (Windurst 2-3, Log ID 2, Mission ID 7), a line-for-line port of
// scripts/missions/windurst/2_3_2_The_Three_Kingdoms_Bastok.lua (quest rework C3 N3). Grohm !pos -18 -11 -27 237; Pius
// !pos 99 -21 -12 237; Patt-Pott !pos 23 -17 42 237. One section (this mission current), an empty reward table (no
// Reward call), all in Metalworks. Grohm: with the mission's Option var 1 plays 425 (progress); else at status 4 423
// (progress) with slot 7 = 1 while RoV's The Path Untraveled is current; at status 5 without an Ounce of Mythril Sand
// 424; else 422 (events). Pius at status 3 plays 355 (progress, parameter 1). Patt-Pott: a trade of exactly one Mythril
// Sand at status 5 plays 255 (progress). Mih Ketto, Moyoyo and Topuru-Kuperu play 267, 264 and 261 (events). Finishes:
// 255 completes the mission and, when that succeeds, takes the trade, sets status 7 and begins The Three Kingdoms; 355
// sets status 4; 423 gives five Pickaxes and sets status 5, or with no room sets Option to 1; 425 gives the Pickaxes,
// sets status 5 and Option 0, or with no room does nothing.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheThreeKingdomsBastok : MissionScript
    {
        public TheThreeKingdomsBastok() : base(XI.Quests.MissionLog.Windurst, "THE_THREE_KINGDOMS_BASTOK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_3_2_The_Three_Kingdoms_Bastok";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Grohm).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(425);
                    }
                    else if (missionStatus == 4)
                    {
                        int onPathUntraveled =
                            q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.ThePathUntraveled ? 1 : 0;

                        return QuestReply.ProgressEvent(423).Option(7, onPathUntraveled);
                    }
                    else if (missionStatus == 5 && !q.Player.HasItem(Item.OnzOfMythrilSand))
                    {
                        return QuestReply.Event(424);
                    }
                    else
                    {
                        return QuestReply.Event(422);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Pius).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(355, 1);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.PattPott).OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 5 &&
                        q.Trade.Matches((Item.OnzOfMythrilSand, 1)))
                    {
                        return QuestReply.ProgressEvent(255);
                    }

                    return null;
                })
                // The staff comment on the errand while the leg runs.
                .Npc(Zone.Metalworks, Npc.Metalworks.MihKetto).OnTrigger(q => QuestReply.Event(267))
                .Npc(Zone.Metalworks, Npc.Metalworks.Moyoyo).OnTrigger(q => QuestReply.Event(264))
                .Npc(Zone.Metalworks, Npc.Metalworks.TopuruKuperu).OnTrigger(q => QuestReply.Event(261))
                .OnEventFinish(Zone.Metalworks, 255, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetMissionStatus(AreaId, 7);
                        q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 355, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.Metalworks, 423, q =>
                {
                    if (!q.Player.GiveItems((Item.Pickaxe, 5)))
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                    else
                    {
                        q.Player.SetMissionStatus(AreaId, 5);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 425, q =>
                {
                    if (q.Player.GiveItems((Item.Pickaxe, 5)))
                    {
                        q.Player.SetMissionStatus(AreaId, 5);
                        q.Mission.SetVar("Option", 0);
                    }
                });
        }
    }
}
