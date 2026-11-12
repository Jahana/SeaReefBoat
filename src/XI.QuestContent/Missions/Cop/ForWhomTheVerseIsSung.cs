// For Whom the Verse is Sung (Promathia 6-1, Log ID 6), a line-for-line port of
// scripts/missions/cop/6_1_For_Whom_the_Verse_is_Sung.lua (quest rework C3). The mission current. Ru'Lud Gardens'
// Pherimociel plays 10046 at Status 0 (its finish sets Status 1) and at Status 1 the replacing 10052. Upper Jeuno's
// _6s1 plays 10011 at Status 1 (Status 2). The Ru'Lud Gardens zone-in plays 10047 at Status 2 and its finish completes
// the mission. A Place to Return is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class ForWhomTheVerseIsSung : MissionScript
    {
        public ForWhomTheVerseIsSung() : base(XI.Quests.MissionLog.CoP, "FOR_WHOM_THE_VERSE_IS_SUNG") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/6_1_For_Whom_the_Verse_is_Sung";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.APlaceToReturn,
            });

            b.State(current: true)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 0)
                    {
                        return QuestReply.ProgressEvent(10046);
                    }

                    return status == 1 ? QuestReply.Event(10052).ReplaceDefault() : null;
                })
                .OnZoneIn(Zone.RuludeGardens, q => q.Mission.Var("Status") == 2 ? QuestReply.Event(10047) : null)
                .OnEventFinish(Zone.RuludeGardens, 10046, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.RuludeGardens, 10047, q => q.Mission.Complete())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s1).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(10011) : null)
                .OnEventFinish(Zone.UpperJeuno, 10011, q => q.Mission.SetVar("Status", 2));
        }
    }
}
