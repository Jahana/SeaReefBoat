// Whispers of Dawn (WotG 47, Log ID 5, Mission ID 46), a line-for-line port of
// scripts/missions/wotg/47_Whispers_of_Dawn.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239
// 89. One section (the mission current), in Grauberg [S]: the Veridical Conflux plays the progress event 26 (89, 23,
// 1756, 0, 0, 8323073, 0, 1 when a main or sub weapon is equipped else 0); its finish with option 1 sets A Dreamy
// Interlude's Timer to the next Vana'diel day and its mustZone, then completes the mission (A Dreamy Interlude
// current); any other option does nothing.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class WhispersOfDawn : MissionScript
    {
        public WhispersOfDawn() : base(XI.Quests.MissionLog.WoTG, "WHISPERS_OF_DAWN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/47_Whispers_of_Dawn";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ADreamyInterlude,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    int hasWeapons = (q.Player.EquipId(Slot.Main) != 0 || q.Player.EquipId(Slot.Sub) != 0) ? 1 : 0;

                    return QuestReply.ProgressEvent(26, 89, 23, 1756, 0, 0, 8323073, 0, hasWeapons);
                })
                .OnEventFinish(Zone.GraubergS, 26, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetMissionVar(AreaId, Mission.Wotg.ADreamyInterlude, "Timer",
                            q.Player.VanadielUniqueDay + 1);
                        q.Player.SetMissionMustZone(AreaId, Mission.Wotg.ADreamyInterlude);

                        q.Mission.Complete();
                    }
                });
        }
    }
}
