// Pillar of Hope (WotG 43, Log ID 5, Mission ID 42), a line-for-line port of
// scripts/missions/wotg/43_Pillar_of_Hope.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239 89.
// One section (the mission current), in Grauberg [S]: the Veridical Conflux plays the progress event 28 (89, 23, 1743,
// Option (1 once declined), 0, 0, 0, 1 when a main or sub weapon is equipped else 0); its finish with option 1
// completes the mission (Glimmer of Life current) and then sets Glimmer of Life's Timer to the next Vana'diel day and
// its mustZone; with any other option it sets Option 1.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class PillarOfHope : MissionScript
    {
        public PillarOfHope() : base(XI.Quests.MissionLog.WoTG, "PILLAR_OF_HOPE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/43_Pillar_of_Hope";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.GlimmerOfLife,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    int hasWeapons = (q.Player.EquipId(Slot.Main) != 0 || q.Player.EquipId(Slot.Sub) != 0) ? 1 : 0;
                    int isRepeated = q.Mission.Var("Option");

                    return QuestReply.ProgressEvent(28, 89, 23, 1743, isRepeated, 0, 0, 0, hasWeapons);
                })
                .OnEventFinish(Zone.GraubergS, 28, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Mission.Complete();

                        q.Player.SetMissionVar(AreaId, Mission.Wotg.GlimmerOfLife, "Timer",
                            q.Player.VanadielUniqueDay + 1);
                        q.Player.SetMissionMustZone(AreaId, Mission.Wotg.GlimmerOfLife);
                    }
                    else
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                });
        }
    }
}
