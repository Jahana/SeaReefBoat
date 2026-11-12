// A Dreamy Interlude (WotG 48, Log ID 5, Mission ID 47), a line-for-line port of
// scripts/missions/wotg/48_A_Dreamy_Interlude.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749
// 585.239 89. One section (the mission current), in Grauberg [S]: with no mustZone and the Timer at or before the
// Vana'diel day, the Veridical Conflux plays the progress event 36 (89, 1, 596904, 1000, 0, 8323089, 0, 0), whose
// finish completes the mission (Cait in the Woods current); otherwise it plays the progress event 27 (89, 12354, 59449,
// 120, 0, 8323089, 0, 0) once per zone (Whispers of Dawn's finish sets both).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ADreamyInterlude : MissionScript
    {
        public ADreamyInterlude() : base(XI.Quests.MissionLog.WoTG, "A_DREAMY_INTERLUDE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/48_A_Dreamy_Interlude";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.CaitInTheWoods,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    if (!q.Mission.MustZone &&
                        q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(36, 89, 1, 596904, 1000, 0, 8323089, 0, 0);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(27, 89, 12354, 59449, 120, 0, 8323089, 0, 0).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.GraubergS, 36, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
