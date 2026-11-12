// Unraveling Reason (ToAU 40, Log ID 4, Mission ID 39), a line-for-line port of
// scripts/missions/toau/40_Unraveling_Reason.lua (quest rework C3 T4). Pherimociel !pos -31.627 1.002 67.956 243. One
// section (the mission current). Aht Urhgan Whitegate: Naja Salaheem plays the plain event 3148. Ru'Lude Gardens:
// Pherimociel at status 0 plays the progress event 10098 when the mustZone is clear and VanadielUniqueDay is at or past
// the Timer var (Allied Rumblings' finish sets it), else the plain event 10099 once (the seenCS local var 0); at any
// other status nothing. 10098's finish with option 1 sets status 1 and sets the char down at -200.036 -10 79.948
// (rotation 254) in Wajaom Woodlands; 10099's finish sets seenCS and the mustZone. Wajaom Woodlands: the zone-in plays
// 11 at status 1, 21 at status 2, 22 at status 3; the updates of 11 and 21 with option 0 answer 0, 4, 0, 0, 0, 0, 0, 3;
// the finish of 11 sets status 2 and of 21 status 3, each setting the char down at the same spot again; 22's finish
// completes the mission (Light of Judgment current, the Endymion Paratrooper title). The Lua passes a ninth updateEvent
// operand, always 0; the port drops it (the engine reads eight, so nothing changes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class UnravelingReason : MissionScript
    {
        public UnravelingReason() : base(XI.Quests.MissionLog.ToAU, "UNRAVELING_REASON") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/40_Unraveling_Reason";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.EndymionParatrooper,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.LightOfJudgment,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3148, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                        {
                            return QuestReply.ProgressEvent(10098); // Ship is ready.
                        }
                        // Optional cutscene. Plays one time per zone until time is up. FORCES zoning.
                        else if (q.Player.LocalVar("Mission[4][39]seenCS") == 0)
                        {
                            return QuestReply.Event(10099); // Ship isn't ready.
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 10098, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                        q.Player.SetPos(-200.036, -10, 79.948, 254, Zone.WajaomWoodlands);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10099, q =>
                {
                    q.Player.SetLocalVar("Mission[4][39]seenCS", 1);
                    q.Mission.SetMustZone();
                })
                .OnZoneIn(Zone.WajaomWoodlands, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1) return QuestReply.Event(11);
                    if (missionStatus == 2) return QuestReply.Event(21);
                    if (missionStatus == 3) return QuestReply.Event(22);

                    return null;
                })
                .OnEventUpdate(Zone.WajaomWoodlands, 11, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 4, 0, 0, 0, 0, 0, 3);
                    }
                })
                .OnEventUpdate(Zone.WajaomWoodlands, 21, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 4, 0, 0, 0, 0, 0, 3);
                    }
                })
                .OnEventFinish(Zone.WajaomWoodlands, 11, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.SetPos(-200.036, -10, 79.948, 254, Zone.WajaomWoodlands);
                })
                .OnEventFinish(Zone.WajaomWoodlands, 21, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.SetPos(-200.036, -10, 79.948, 254, Zone.WajaomWoodlands);
                })
                .OnEventFinish(Zone.WajaomWoodlands, 22, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
