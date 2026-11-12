// The Lost City (Promathia 2-2, Log ID 6, Mission ID 218), a line-for-line port of
// scripts/missions/cop/2_2_The_Lost_City.lua (quest rework C3). Despachiaire !pos 108 -40 -83 26; Sewer Entrance
// !pos 28 -12 44 26. Section 1 (the mission current), all in Tavnazian Safehold: Despachiaire at Status 0 plays 102
// (its finish sets Status 1), else 106 with 1 replacing the default; at Status 1 the Sewer Entrance _0q1 plays 103
// (its finish completes the mission), Arquil 291, Justinius 360 and Liphatte 301, each replacing the default. Section 2
// (2-2 completed, Darkness Named not): Arquil 291 and Despachiaire 106 (no parameter) replace their defaults. Distant
// Beliefs is next; no title. Kept as the Lua has it: Justinius is matched by name, so both of the zone's NPCs of that
// name answer.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheLostCity : MissionScript
    {
        public TheLostCity() : base(XI.Quests.MissionLog.CoP, "THE_LOST_CITY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/2_2_The_Lost_City";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.DistantBeliefs,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0q1).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(103) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Arquil).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(291).ReplaceDefault() : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                    q.Mission.Var("Status") == 0
                        ? QuestReply.ProgressEvent(102)
                        : QuestReply.Event(106, 1).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(360).ReplaceDefault() : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Liphatte).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(301).ReplaceDefault() : null)
                .OnEventFinish(Zone.TavnazianSafehold, 102, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.TavnazianSafehold, 103, q => q.Mission.Complete());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheLostCity) &&
                    !q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DarknessNamed))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Arquil)
                    .OnTrigger(QuestReply.Event(291).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire)
                    .OnTrigger(QuestReply.Event(106).ReplaceDefault());
        }
    }
}
