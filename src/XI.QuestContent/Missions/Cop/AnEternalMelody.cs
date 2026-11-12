// An Eternal Melody (Promathia 2-4, Log ID 6, Mission ID 238), a line-for-line port of
// scripts/missions/cop/2_4_An_Eternal_Melody.lua (quest rework C3). Walnut Door !pos 111 -41 41 26; Justinius !pos 76
// -34 68 26; Dilapidated Gate !pos 260 9 -435 25. One section (the mission current): Tavnazian Safehold's Walnut Door
// _0qa plays 104 at Status 0 (its finish gives the Mysterious Amulet and sets Status 1); Justinius plays 127 at Status
// 1, else 125, replacing the default; Parelbriaux plays 126 at Status 1, replacing the default; trigger area 1 at
// Status 2 plays 105 (its finish completes the mission). Misareaux Coast's gate _0p0 plays cutscene 5 at Status 1 (its
// finish sets Status 2). Ancient Vows is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class AnEternalMelody : MissionScript
    {
        public AnEternalMelody() : base(XI.Quests.MissionLog.CoP, "AN_ETERNAL_MELODY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/2_4_An_Eternal_Melody";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.AncientVows,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0qa).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(104) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 1
                        ? QuestReply.Event(127).ReplaceDefault()
                        : QuestReply.Event(125).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Parelbriaux).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(126).ReplaceDefault() : null)
                .OnTriggerAreaEnter(Zone.TavnazianSafehold, 1, q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressEvent(105) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 104, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MysteriousAmulet);
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 105, q => q.Mission.Complete())
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p0).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Cutscene(5) : null)
                .OnEventFinish(Zone.MisareauxCoast, 5, q => q.Mission.SetVar("Status", 2));
        }
    }
}
