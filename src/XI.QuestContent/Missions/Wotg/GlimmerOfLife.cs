// Glimmer of Life (WotG 44, Log ID 5, Mission ID 43), a line-for-line port of
// scripts/missions/wotg/44_Glimmer_of_Life.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239
// 89. One section (the mission current), in Grauberg [S]: with no mustZone and the Timer at or before the Vana'diel
// day, the Veridical Conflux plays the progress event 34 (89, 23, 1756, 0, 0, 0, 0, 0), whose finish completes the
// mission (Time Slips Away current); otherwise the trigger falls through (Pillar of Hope's finish sets both).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class GlimmerOfLife : MissionScript
    {
        public GlimmerOfLife() : base(XI.Quests.MissionLog.WoTG, "GLIMMER_OF_LIFE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/44_Glimmer_of_Life";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TimeSlipsAway,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    if (!q.Mission.MustZone &&
                        q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        // the Lua's note: no weapons need to be removed for this cutscene
                        return QuestReply.ProgressEvent(34, 89, 23, 1756, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.GraubergS, 34, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
