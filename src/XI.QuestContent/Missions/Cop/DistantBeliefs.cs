// Distant Beliefs (Promathia 2-3, Log ID 6, Mission ID 228), a line-for-line port of
// scripts/missions/cop/2_3_Distant_Beliefs.lua (quest rework C3). Minotaur !pos 60 0.5 304 27; Iron Gate !pos -70.8
// -1.5 60 27; Ornate Gate !pos -95 -24 60 27; Justinius !pos 76 -34 68 26. Section 1 (the mission current): Tavnazian
// Safehold's Justinius plays 113 at Status 3 (its finish completes the mission), else 123 replacing the default; the
// Phomiuna Aqueducts' Ornate Gate _0r5 plays cutscene 36 at Status 2 (its finish sets Status 3); the Minotaur's death
// at Status 0 sets Status 1 (the wooden ladder's event 35 sets Status 2 in the ladder's own NPC script,
// Phomiuna_Aqueducts/npcs/Wooden_Ladder.lua). Section 2 (2-3 completed): Chemioue 281 and Mengrenaux 271 replace their
// defaults. An Eternal Melody is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class DistantBeliefs : MissionScript
    {
        public DistantBeliefs() : base(XI.Quests.MissionLog.CoP, "DISTANT_BELIEFS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/2_3_Distant_Beliefs";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.AnEternalMelody,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 3
                        ? QuestReply.ProgressEvent(113)
                        : QuestReply.Event(123).ReplaceDefault())
                .OnEventFinish(Zone.TavnazianSafehold, 113, q => q.Mission.Complete())
                .Npc(Zone.PhomiunaAqueducts, Npc.PhomiunaAqueducts._0r5).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressCutscene(36) : null)
                .Npc(Zone.PhomiunaAqueducts, Npc.PhomiunaAqueducts.Minotaur).OnMobDeath(q =>
                {
                    if (q.Mission.Var("Status") == 0) q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.PhomiunaAqueducts, 36, q => q.Mission.SetVar("Status", 3));

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DistantBeliefs))
                // carries over up to the beginning of Ancient Vows
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Chemioue)
                    .OnTrigger(QuestReply.Event(281).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Mengrenaux)
                    .OnTrigger(QuestReply.Event(271).ReplaceDefault());
        }
    }
}
